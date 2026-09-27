using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Files put together from the specimens are sound, and a file saved unchanged still meets the standards it claims.
/// </summary>
public class OperationsTests
{
    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void AssembledFilesPassQpdfCheck(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf();
        byte[] other = SpecimenCatalog.All[0].Build().ExportPdf();

        byte[] assembled = PdfFile.Open(pdf)
            .Append(PdfFile.Open(other))
            .Overlay(PdfFile.Open(other), onto: "1")
            .Underlay(PdfFile.Open(pdf), onto: "last")
            .KeepPages("last-1")
            .ToArray();

        Assert.Contains("No syntax or stream encoding errors found", Qpdf.Check(assembled), StringComparison.Ordinal);
    }

    [Fact]
    public void AFileSavedUnchangedStillMeetsItsStandards()
    {
        TestFonts.EnsureRegistered();
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (Specimen specimen in SpecimenCatalog.All)
        {
            Document document = specimen.Build();
            document.Info.Title ??= specimen.Name;
            document.Info.Language ??= "en";

            byte[] exported = document.ExportPdf(new PdfExportOptions
            {
                Conformance = PdfAConformance.PdfA2A,
                Accessibility = PdfUAConformance.PdfUA1,
                ImageProcessor = SkiaImageProcessor.Instance,
            });

            files.Add(specimen.Name, PdfFile.Open(exported).ToArray());
        }

        string[] broken = VeraPdf.Validate(files)
            .SelectMany(file => file.Value.Select(rule => $"{file.Key}: {rule}"))
            .ToArray();

        Assert.True(broken.Length == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }
}
