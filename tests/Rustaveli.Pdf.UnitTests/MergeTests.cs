namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Documents merged into one: every page of each in turn, numbered on from one to the next or each from 1.
/// </summary>
public class MergeTests
{
    /// <summary>A document of <paramref name="pages"/> pages, each footed "name folio/count".</summary>
    private static Document Pages(string name, int pages) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(200, 100);
        section.RunningFoot().Text(text =>
        {
            text.Run(name + " ");
            text.Folio();
            text.Run("/");
            text.PageCount();
        });
        section.Body().Stack(stack =>
        {
            for (int page = 1; page <= pages; page++)
            {
                if (page > 1)
                    stack.Add().NewPage();

                stack.Add().Text("Body");
            }
        });
    }));

    /// <summary>The foot of every page, as drawn.</summary>
    private static List<string> Feet(Document document) =>
        LayoutHarness.Render(document).Pages
            .Select(page => string.Concat(page.Operations.OfType<TextOperation>().Select(operation => operation.Text).Where(text => text != "Body")))
            .ToList();

    [Fact]
    public void MergedDocumentsAreNumberedOnFromOneToTheNext()
    {
        Document merged = Document.Merge(Pages("A", 2), Pages("B", 3));

        Assert.Equal(["A 1/5", "A 2/5", "B 3/5", "B 4/5", "B 5/5"], Feet(merged));
    }

    [Fact]
    public void MergedDocumentsNumberedSeparatelyEachCountFromOne()
    {
        Document merged = Document.Merge(Pages("A", 2), Pages("B", 3)).NumberPartsSeparately();

        Assert.Equal(["A 1/2", "A 2/2", "B 1/3", "B 2/3", "B 3/3"], Feet(merged));
    }

    [Fact]
    public void ADocumentMergedTwiceIsSetInFullBothTimes()
    {
        Document once = Pages("A", 2);

        Assert.Equal(["A 1/4", "A 2/4", "A 3/4", "A 4/4"], Feet(Document.Merge(once, once)));
        Assert.Equal(["A 1/2", "A 2/2"], Feet(once));
    }

    [Fact]
    public void AMergedDocumentDescribesItselfAsTheFirstDoes()
    {
        Document first = Pages("A", 1);
        first.Info.Title = "Annual report";
        first.Info.Author = "Finance";
        first.Info.Subject = "Results";
        first.Info.Keywords = "annual";
        first.Info.Creator = "Reports";
        first.Info.Producer = "Tests";
        first.Info.Language = "en-GB";
        first.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        first.Info.ModificationDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        Document second = Pages("B", 1);
        second.Info.Title = "Appendix";

        DocumentInfo info = Document.Merge(first, second).Info;

        Assert.Equal(
            ("Annual report", "Finance", "Results", "annual", "Reports", "Tests", "en-GB"),
            (info.Title, info.Author, info.Subject, info.Keywords, info.Creator, info.Producer, info.Language));
        Assert.Equal(first.Info.CreationDate, info.CreationDate);
        Assert.Equal(first.Info.ModificationDate, info.ModificationDate);
    }

    [Fact]
    public void AMergedDocumentMayTakeThePagesItsDocumentsAllow()
    {
        Document first = Pages("A", 1);
        first.PageLimit = 3;
        Document second = Pages("B", 1);
        second.PageLimit = int.MaxValue;

        Assert.Equal(10_000 + 3, Document.Merge(Pages("C", 1), first).PageLimit);
        Assert.Equal(int.MaxValue, Document.Merge(first, second).PageLimit);
    }

    [Fact]
    public void ContentComposedLaterNamesStylesFromItsOwnDocument()
    {
        Document Styled(float size) => Document.Compose(composition =>
        {
            composition.Styles.DefineType("Size", style => style.WithPointSize(size));
            composition.Section(section => section.Body().ComposeLater(later => later.Text(text => text.Run("x").Style("Size"))));
        });

        List<float> sizes = LayoutHarness.Render(Document.Merge(Styled(10), Styled(20))).Pages
            .SelectMany(page => page.Operations.OfType<TextOperation>())
            .Select(text => text.Style.PointSize)
            .ToList();

        Assert.Equal([10f, 20f], sizes);
    }

    [Fact]
    public void AMergeMergedAgainKeepsTheStylesOfEachOfItsDocuments()
    {
        Document Styled(float size) => Document.Compose(composition =>
        {
            composition.Styles.DefineType("Size", style => style.WithPointSize(size));
            composition.Section(section => section.Body().ComposeLater(later => later.Text(text => text.Run("x").Style("Size"))));
        });

        List<float> sizes = LayoutHarness.Render(Document.Merge(Document.Merge(Styled(10), Styled(20)), Styled(30))).Pages
            .SelectMany(page => page.Operations.OfType<TextOperation>())
            .Select(text => text.Style.PointSize)
            .ToList();

        Assert.Equal([10f, 20f, 30f], sizes);
    }

    [Fact]
    public void AMergeMergedAgainNumbersEachOfItsDocumentsSeparately()
    {
        Document merged = Document.Merge(Document.Merge(Pages("A", 2), Pages("B", 1)), Pages("C", 2)).NumberPartsSeparately();

        Assert.Equal(3, merged.PartCount);
        Assert.Equal(["A 1/2", "A 2/2", "B 1/1", "C 1/2", "C 2/2"], Feet(merged));
    }

    [Fact]
    public void MergingNeedsDocuments()
    {
        Assert.Throws<ArgumentNullException>(() => Document.Merge(null!));
        Assert.Throws<ArgumentException>(() => Document.Merge());
        Assert.Throws<ArgumentException>(() => Document.Merge(Pages("A", 1), null!));
    }

    [Fact]
    public void ASingleDocumentIsOnePart()
    {
        Document merged = Document.Merge(Pages("A", 1), Pages("B", 1), Pages("C", 1));

        Assert.Equal(1, Pages("A", 1).PartCount);
        Assert.Equal(3, merged.PartCount);
        Assert.Equal([0, 1, 2], Enumerable.Range(0, 3).Select(merged.PartOf));
        Assert.Same(merged, merged.NumberPartsSeparately());
    }
}
