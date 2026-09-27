using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// The document outline as a viewer reads it, and the document's language.
/// </summary>
public class OutlineOutputTests
{
    private static byte[] Export(Action<StackComposer> compose, Action<DocumentInfo>? info = null, bool compress = true)
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 100);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Stack(compose);
        }));

        info?.Invoke(document.Info);
        return document.ExportPdf(new PdfExportOptions { Compress = compress });
    }

    [Fact]
    public void BookmarksNestUnderTheNearestShallowerOne()
    {
        byte[] pdf = Export(stack =>
        {
            stack.Add().Bookmark("One").Text("One");
            stack.Add().Bookmark("One point one", 2).Text("1.1");
            stack.Add().Bookmark("One point one point one", 3).Height(80).Text("1.1.1");
            stack.Add().Bookmark("One point two", 2).Text("1.2");
            stack.Add().Bookmark("Two").Text("Two");
            stack.Add().Bookmark("Two, deeper", 3).Text("Deeper");
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);
        Assert.True(parsed.TryGetBookmarks(out Bookmarks? bookmarks));

        BookmarkNode[] roots = bookmarks!.Roots.ToArray();
        Assert.Equal(["One", "Two"], roots.Select(node => node.Title));
        Assert.Equal(["One point one", "One point two"], roots[0].Children.Select(node => node.Title));
        Assert.Equal(["One point one point one"], roots[0].Children[0].Children.Select(node => node.Title));
        Assert.Equal(["Two, deeper"], roots[1].Children.Select(node => node.Title));
    }

    [Fact]
    public void ABookmarkLeadsToThePageAndPlaceItsContentStarts()
    {
        byte[] pdf = Export(stack =>
        {
            stack.Add().Height(90).Blank();
            stack.Add().Bookmark("Second page").Text("Here");
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);
        parsed.TryGetBookmarks(out Bookmarks? bookmarks);

        DocumentBookmarkNode node = Assert.IsType<DocumentBookmarkNode>(Assert.Single(bookmarks!.Roots));
        Assert.Equal(2, node.PageNumber);
        Assert.Equal(100, node.Destination.Coordinates.Top!.Value, 1);
    }

    [Fact]
    public void ADocumentWithBookmarksOpensShowingThem()
    {
        string pdf = Encoding.Latin1.GetString(Export(stack => stack.Add().Bookmark("Only").Text("x"), compress: false));

        Assert.Matches(@"/PageMode\s*/UseOutlines", pdf);
        Assert.Matches(@"/Count\s*1", pdf);
    }

    [Fact]
    public void ADocumentWithoutBookmarksHasNoOutline()
    {
        string pdf = Encoding.Latin1.GetString(Export(stack => stack.Add().Text("x"), compress: false));

        Assert.DoesNotContain("/Outlines", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/PageMode", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentsLanguageIsWritten()
    {
        string pdf = Encoding.Latin1.GetString(Export(stack => stack.Add().Text("x"), info => info.Language = " ka-GE ", compress: false));

        Assert.Matches(@"/Lang\s*\(ka-GE\)", pdf);
    }

    [Fact]
    public void NoLanguageIsWrittenUnlessGiven()
    {
        string pdf = Encoding.Latin1.GetString(Export(stack => stack.Add().Text("x"), info => info.Language = " ", compress: false));

        Assert.DoesNotContain("/Lang", pdf, StringComparison.Ordinal);
    }
}
