using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>Every specimen must pass qpdf's structural check with no errors and no warnings.</summary>
public class StructureTests
{
    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void PassesQpdfCheck(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf();

        string report = Qpdf.Check(pdf);

        Assert.Contains("No syntax or stream encoding errors found", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every specimen can be written as PDF/A — every glyph found, every image in RGB — and the file is sound.
    /// </summary>
    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void PassesQpdfCheckAsPdfA(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf(new PdfExportOptions
        {
            Conformance = PdfAConformance.PdfA2U,
            ImageProcessor = SkiaImageProcessor.Instance,
        });

        string report = Qpdf.Check(pdf);

        Assert.Contains("No syntax or stream encoding errors found", report, StringComparison.Ordinal);
    }
}
