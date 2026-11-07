using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

public sealed record PdfSnapshot(IReadOnlyList<PageSnapshot> Pages)
{
    public int PageCount => Pages.Count;

    public IEnumerable<string> AllWords => Pages.SelectMany(page => page.Words).Select(word => word.Text);

    public static PdfSnapshot Capture(byte[] pdf)
    {
        using PdfDocument pdfDocument = PdfDocument.Open(pdf);
        List<PageSnapshot> pages = pdfDocument.GetPages()
            .Select(page => new PageSnapshot(
                page.Width,
                page.Height,
                page.GetWords()
                    .Select(word => new WordSnapshot(
                        word.Text, word.BoundingBox.Left, page.Height - word.BoundingBox.Top))
                    .ToList()))
            .ToList();
        return new PdfSnapshot(pages);
    }
}
