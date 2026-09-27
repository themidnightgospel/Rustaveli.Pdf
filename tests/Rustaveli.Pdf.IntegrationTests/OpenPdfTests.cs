namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Exporting a document and opening it in the system's viewer, with the viewer stood in for.
/// </summary>
[Collection(nameof(OpenPdfTests))]
public class OpenPdfTests : IDisposable
{
    private readonly Action<string> _open = PdfExport.Open;
    private readonly List<string> _opened = [];

    public OpenPdfTests() => PdfExport.Open = _opened.Add;

    public void Dispose()
    {
        PdfExport.Open = _open;

        foreach (string path in _opened)
            File.Delete(path);
    }

    private static Document Titled(string? title)
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Text("Hello")));
        document.Info.Title = title;
        return document;
    }

    [Fact]
    public void TheDocumentIsWrittenThenOpened()
    {
        string path = Titled("Quarterly report: Q3/2026").ExportPdfAndOpen();

        Assert.Equal([path], _opened);
        Assert.StartsWith(Path.GetTempPath(), path, StringComparison.Ordinal);
        Assert.StartsWith("Quarterly-report--Q3-2026-", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 5), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("***")]
    public void AnUntitledDocumentIsCalledDocument(string? title) =>
        Assert.StartsWith("document-", Path.GetFileName(Titled(title).ExportPdfAndOpen()), StringComparison.Ordinal);

    [Fact]
    public void ALongTitleIsShortened() =>
        Assert.Equal(40 + "-12345678.pdf".Length, Path.GetFileName(Titled(new string('a', 100)).ExportPdfAndOpen()).Length);

    [Fact]
    public void ADocumentIsNeeded() =>
        Assert.Throws<ArgumentNullException>(() => PdfExport.ExportPdfAndOpen(null!));
}
