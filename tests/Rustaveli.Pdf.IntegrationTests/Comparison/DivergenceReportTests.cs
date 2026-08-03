using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// Diagnostic output quantifying how far this engine's layout sits from QuestPDF's for the shared recipes.
/// </summary>
/// <remarks>
/// Not an assertion of equivalence — it exists to make the divergence visible and to justify the tolerances the
/// real comparison tests use. Run with detailed verbosity to read the report.
/// </remarks>
public class DivergenceReportTests(ITestOutputHelper output)
{
    [Fact]
    public void ReportTextFlowDivergence() =>
        Report("text flow", Recipes.RustaveliTextFlow(), Recipes.QuestTextFlow());

    [Fact]
    public void ReportPaginatedDivergence() =>
        Report("paginated", Recipes.RustaveliPaginated(), Recipes.QuestPaginated());

    [Fact]
    public void ReportTableDivergence() =>
        Report("table", Recipes.RustaveliTable(), Recipes.QuestTable());

    private void Report(string name, byte[] ours, byte[] theirs)
    {
        PdfSnapshot mine = PdfSnapshot.Capture(ours);
        PdfSnapshot reference = PdfSnapshot.Capture(theirs);

        output.WriteLine($"=== {name} ===");

        // File size is a proxy for font handling: Skia embeds whole typefaces, so a large gap against QuestPDF
        // would indicate it is subsetting fonts where we are not.
        output.WriteLine($"bytes          : ours={ours.Length:N0} quest={theirs.Length:N0}");
        output.WriteLine($"pages          : ours={mine.PageCount} quest={reference.PageCount}");
        output.WriteLine($"words          : ours={mine.AllWords.Count()} quest={reference.AllWords.Count()}");
        output.WriteLine($"word sequences : {(mine.AllWords.SequenceEqual(reference.AllWords) ? "identical" : "DIFFER")}");

        int pages = Math.Min(mine.PageCount, reference.PageCount);

        for (int index = 0; index < pages; index++)
        {
            PageSnapshot minePage = mine.Pages[index];
            PageSnapshot referencePage = reference.Pages[index];
            int count = Math.Min(minePage.Words.Count, referencePage.Words.Count);

            if (count == 0)
                continue;

            double deltaLeft = 0d;
            double deltaTop = 0d;
            double maxLeft = 0d;
            double maxTop = 0d;

            for (int word = 0; word < count; word++)
            {
                double left = Math.Abs(minePage.Words[word].Left - referencePage.Words[word].Left);
                double top = Math.Abs(minePage.Words[word].Top - referencePage.Words[word].Top);

                deltaLeft += left;
                deltaTop += top;
                maxLeft = Math.Max(maxLeft, left);
                maxTop = Math.Max(maxTop, top);
            }

            output.WriteLine(
                $"page {index + 1,-3} words ours={minePage.Words.Count,-4} quest={referencePage.Words.Count,-4} " +
                $"meanDx={deltaLeft / count:F2} maxDx={maxLeft:F2} meanDy={deltaTop / count:F2} maxDy={maxTop:F2}");
        }

        if (!mine.AllWords.SequenceEqual(reference.AllWords))
        {
            List<string> mineWords = mine.AllWords.ToList();
            List<string> referenceWords = reference.AllWords.ToList();
            int limit = Math.Min(mineWords.Count, referenceWords.Count);

            for (int index = 0; index < limit; index++)
            {
                if (mineWords[index] == referenceWords[index])
                    continue;

                output.WriteLine($"first difference at word {index}: ours='{mineWords[index]}' quest='{referenceWords[index]}'");
                break;
            }
        }
    }
}
