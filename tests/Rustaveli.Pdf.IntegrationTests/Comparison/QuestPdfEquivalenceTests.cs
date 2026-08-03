namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// Renders identical recipes through this library and through QuestPDF, then compares what a reader recovers
/// from each PDF.
/// </summary>
/// <remarks>
/// QuestPDF is used purely as an independent oracle: it is a mature engine whose pagination and text layout are
/// widely trusted, so agreeing with it on page breaks and word placement is strong evidence this engine is
/// correct. The comparison is deliberately behavioural rather than byte-level — two PDF producers never emit
/// identical bytes for the same document.
///
/// The QuestPDF reference is pinned to version 2026.5.0, the last release distributed under an MIT grant.
/// Releases from 2026.6.0 onwards carry a source-available licence whose restrictions forbid using the software
/// to build a competing PDF library, which would cover this test suite.
/// </remarks>
public class QuestPdfEquivalenceTests
{
    /// <summary>
    /// Permitted horizontal drift per word, in points. Both engines measure through Skia with the same font, so
    /// the residual comes from how each accumulates advance widths across a line. Roughly a third of a character
    /// at the sizes under test.
    /// </summary>
    private const double HorizontalTolerance = 4.0;

    /// <summary>
    /// Permitted vertical drift per word, in points. Baselines agree to within a rounding step, so this is kept
    /// tight: a genuine line-height disagreement would be an order of magnitude larger.
    /// </summary>
    private const double VerticalTolerance = 0.5;

    public static TheoryData<string, Func<byte[]>, Func<byte[]>> Recipes => new()
    {
        { "text flow", Comparison.Recipes.RustaveliTextFlow, Comparison.Recipes.QuestTextFlow },
        { "header, footer and page numbers", Comparison.Recipes.RustaveliPaginated, Comparison.Recipes.QuestPaginated },
        { "table with repeating header", Comparison.Recipes.RustaveliTable, Comparison.Recipes.QuestTable }
    };

    [Theory]
    [MemberData(nameof(Recipes))]
    public void BreaksPagesAtTheSamePlaces(string name, Func<byte[]> ours, Func<byte[]> theirs)
    {
        PdfSnapshot mine = PdfSnapshot.Capture(ours());
        PdfSnapshot reference = PdfSnapshot.Capture(theirs());

        Assert.True(
            reference.PageCount == mine.PageCount,
            $"[{name}] produced {mine.PageCount} pages but QuestPDF produced {reference.PageCount}.");

        // Matching totals could still hide content moving between pages, so compare the per-page split too.
        Assert.True(
            reference.Pages.Select(page => page.Words.Count).SequenceEqual(mine.Pages.Select(page => page.Words.Count)),
            $"[{name}] distributed words across pages as " +
            $"[{string.Join(", ", mine.Pages.Select(page => page.Words.Count))}] but QuestPDF used " +
            $"[{string.Join(", ", reference.Pages.Select(page => page.Words.Count))}].");
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void ProducesTheSameWordsInTheSameOrder(string name, Func<byte[]> ours, Func<byte[]> theirs)
    {
        PdfSnapshot mine = PdfSnapshot.Capture(ours());
        PdfSnapshot reference = PdfSnapshot.Capture(theirs());

        List<string> mineWords = mine.AllWords.ToList();
        List<string> referenceWords = reference.AllWords.ToList();

        (string Ours, string Theirs, int Index) firstDifference = mineWords
            .Zip(referenceWords, (a, b) => (Ours: a, Theirs: b))
            .Select((pair, index) => (pair.Ours, pair.Theirs, Index: index))
            .FirstOrDefault(pair => pair.Ours != pair.Theirs, (null!, null!, -1));

        Assert.True(
            mineWords.SequenceEqual(referenceWords),
            firstDifference.Index >= 0
                ? $"[{name}] word {firstDifference.Index} is '{firstDifference.Ours}' but QuestPDF wrote '{firstDifference.Theirs}'."
                : $"[{name}] produced {mineWords.Count} words but QuestPDF produced {referenceWords.Count}.");
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void PlacesEveryWordInTheSamePosition(string name, Func<byte[]> ours, Func<byte[]> theirs)
    {
        PdfSnapshot mine = PdfSnapshot.Capture(ours());
        PdfSnapshot reference = PdfSnapshot.Capture(theirs());

        Assert.Equal(reference.PageCount, mine.PageCount);

        for (int pageIndex = 0; pageIndex < reference.PageCount; pageIndex++)
        {
            PageSnapshot minePage = mine.Pages[pageIndex];
            PageSnapshot referencePage = reference.Pages[pageIndex];

            Assert.Equal(referencePage.Words.Count, minePage.Words.Count);

            for (int wordIndex = 0; wordIndex < referencePage.Words.Count; wordIndex++)
            {
                WordSnapshot mineWord = minePage.Words[wordIndex];
                WordSnapshot referenceWord = referencePage.Words[wordIndex];

                double horizontal = Math.Abs(mineWord.Left - referenceWord.Left);
                double vertical = Math.Abs(mineWord.Top - referenceWord.Top);

                Assert.True(
                    horizontal <= HorizontalTolerance,
                    $"[{name}] page {pageIndex + 1} word {wordIndex} '{mineWord.Text}' sits {horizontal:F2}pt from " +
                    $"QuestPDF's horizontal position (ours {mineWord.Left:F2}, QuestPDF {referenceWord.Left:F2}).");

                Assert.True(
                    vertical <= VerticalTolerance,
                    $"[{name}] page {pageIndex + 1} word {wordIndex} '{mineWord.Text}' sits {vertical:F2}pt from " +
                    $"QuestPDF's vertical position (ours {mineWord.Top:F2}, QuestPDF {referenceWord.Top:F2}).");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void ProducesPagesOfTheSameSize(string name, Func<byte[]> ours, Func<byte[]> theirs)
    {
        PdfSnapshot mine = PdfSnapshot.Capture(ours());
        PdfSnapshot reference = PdfSnapshot.Capture(theirs());

        for (int index = 0; index < Math.Min(mine.PageCount, reference.PageCount); index++)
        {
            Assert.True(
                Math.Abs(reference.Pages[index].Width - mine.Pages[index].Width) < 0.5
                && Math.Abs(reference.Pages[index].Height - mine.Pages[index].Height) < 0.5,
                $"[{name}] page {index + 1} measures {mine.Pages[index].Width:F1}x{mine.Pages[index].Height:F1} " +
                $"but QuestPDF produced {reference.Pages[index].Width:F1}x{reference.Pages[index].Height:F1}.");
        }
    }

    [Fact]
    public void RepeatsTheTableHeaderOnEveryPageJustAsQuestPdfDoes()
    {
        // Regression guard for a defect this comparison surfaced: header cells hold text, text tracks how many
        // of its lines have been drawn, and so the band silently vanished after the first page.
        PdfSnapshot mine = PdfSnapshot.Capture(Comparison.Recipes.RustaveliTable());

        Assert.True(mine.PageCount > 1, "The table recipe must span several pages for this to mean anything.");

        foreach (PageSnapshot page in mine.Pages)
            Assert.Contains("Code", page.Words.Select(word => word.Text));
    }
}
