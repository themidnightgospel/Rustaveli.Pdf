using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

public sealed record PdfSnapshot(IReadOnlyList<PageSnapshot> Pages)
{
    public int PageCount => Pages.Count;

    public IEnumerable<string> AllWords => from word in Pages.SelectMany(page => page.Words)
        select word.Text;

    public static PdfSnapshot Capture(byte[] pdf)
    {
        using PdfDocument pdfDocument = PdfDocument.Open(pdf);
        List<PageSnapshot> pages = (from page in pdfDocument.GetPages()
            select new PageSnapshot(page.Width, page.Height, (from word in page.GetWords()
                select new WordSnapshot(word.Text, word.BoundingBox.Left, page.Height - word.BoundingBox.Top)).ToList())).ToList();
        return new PdfSnapshot(pages);
    }
}
