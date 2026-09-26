using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;
using Rustaveli.Pdf.Skia;

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
}
