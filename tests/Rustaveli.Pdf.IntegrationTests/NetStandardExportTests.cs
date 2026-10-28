#if !NET
namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Exporting on the build of the library that .NET Framework, .NET 8 and .NET 9 use, which the benchmarks, run on
/// .NET 10 alone, never measure.
/// </summary>
public class NetStandardExportTests
{
    private static readonly string[] Words =
    [
        "layout", "measure", "column", "margin", "gutter", "leading", "paragraph", "statement", "account", "balance",
        "period", "delivery", "reference", "customer", "quantity", "a", "the", "of", "and", "to", "in", "for"
    ];

    [Fact]
    public void ExportingAReportAllocatesWithinItsBudget()
    {
        // Allocation budget: no benchmark holds this build to a baseline, so this holds the export of an 82-page report
        // to about a quarter more than the 1.85 MB it allocates. That leaves room for small changes, while one on the
        // scale of a string made for every word measured, which once more than doubled a report's allocations on this
        // build, fails it. When a change moves this on purpose, set the new figure and say why in the commit.
        const long Budget = 2_300_000;

        long allocated = (long)MeasuringDomain.Run(ExportAReport, "allocated")[0];

        Assert.True(
            allocated <= Budget,
            $"Exporting the report allocated {allocated:N0} bytes; its budget is {Budget:N0}.");
    }

    /// <summary>Runs in the measuring domain: exports the report once to warm up, then again, measured.</summary>
    private static void ExportAReport()
    {
        Document report = Report();
        report.ExportPdf(Stream.Null);

        long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        report.ExportPdf(Stream.Null);
        long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;

        AppDomain.CurrentDomain.SetData("allocated", allocated);
    }

    /// <summary>Paragraphs of seeded words under a running head and a numbered running foot.</summary>
    private static Document Report() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A4;
        section.Margins = Sides.All(40f);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(10f);
        section.RunningHead().InsetBottom(10f).Text("Report");
        section.RunningFoot().Centered().Text(text =>
        {
            text.Run("Page ");
            text.Folio();
            text.Run(" of ");
            text.PageCount();
        });

        Random random = new Random(1);
        section.Body().Stack(stack =>
        {
            stack.SpaceBetween(6f);

            for (int paragraph = 0; paragraph < 900; paragraph++)
                stack.Add().Text(Paragraph(random));
        });
    }));

    /// <summary>A sentence of 30 to 89 words, drawn at random.</summary>
    private static string Paragraph(Random random) =>
        string.Join(" ", Enumerable.Range(0, random.Next(30, 90)).Select(_ => Words[random.Next(Words.Length)])) + ".";
}
#endif
