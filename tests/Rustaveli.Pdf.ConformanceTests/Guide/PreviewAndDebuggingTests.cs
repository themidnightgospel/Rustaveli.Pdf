using System.Globalization;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/preview-and-debugging.md, as written there.</summary>
public class PreviewAndDebuggingTests
{
    [Fact]
    public void APreviewInsideAnotherProgram()
    {
        using PreviewSession session = DocumentPreview.StartPreview(InvoiceDocument.Compose, new PreviewOptions { OpenBrowser = false });

        Console.WriteLine($"Previewing at {session.Url}");
        session.Refresh();

        Assert.Equal(2, session.Version);
        Assert.Equal("localhost", session.Url.Host);
    }

    [Fact]
    public void SeeingWhereFramesLie()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);

            section.Body().Stack(stack =>
            {
                stack.Add().ShowFrameEdges("Address").Inset(8).Text("12 Rustaveli Avenue, Tbilisi");
                stack.Add().Named("Totals").ShowFrameEdges().Inset(8).Text("Total due: 1,500.00");
            });
        }));

        GuideOutput.Show(document, "frame-edges", trim: true);
        string text = GuideReader.Text(document.ExportPdf());

        Assert.Contains("Address", text, StringComparison.Ordinal);
        Assert.Contains("Total due: 1,500.00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingALayoutFailure()
    {
        // Messages read the same in every culture: this one writes decimals with a comma.
        using CultureScope culture = new CultureScope(new CultureInfo("de-DE"));

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 200);
            section.Margins = Sides.All(20);

            section.Body().Stack(stack =>
            {
                stack.Add().Text("Summary");
                stack.Add().Named("Totals").Inset(10).Height(400).Text("Totals");
            });
        }));

        OversetException failure = Assert.Throws<OversetException>(() => document.ExportPdf());

        // The guide shows the message word for word.
        string guide = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "guide", "preview-and-debugging.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("```text\n" + failure.Message + "\n```", guide, StringComparison.Ordinal);
    }

    /// <summary>What the guide's console program runs; it waits for Ctrl+C, so it is compiled here but never called.</summary>
    internal static void Program()
    {
        DocumentPreview.Preview(InvoiceDocument.Compose);
    }

    public static class InvoiceDocument
    {
        public static Document Compose() => Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);
            section.Body().Text("Invoice 2026-041");
        }));
    }

    /// <summary>Sets the current culture for a test, and puts the previous one back.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture) => CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
