using UglyToad.PdfPig;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>Reads back what a guide's example wrote, for its test to check.</summary>
internal static class GuideReader
{
    /// <summary>Each page's words, in reading order, joined by spaces.</summary>
    public static IReadOnlyList<string> PageTexts(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => string.Join(" ", page.GetWords().Select(word => word.Text))).ToList();
    }

    public static IReadOnlyList<string> PageTexts(string path) => PageTexts(File.ReadAllBytes(path));

    /// <summary>All the words of every page as one text.</summary>
    public static string Text(byte[] pdf) => string.Join(" ", PageTexts(pdf));

    public static string Text(string path) => Text(File.ReadAllBytes(path));
}
