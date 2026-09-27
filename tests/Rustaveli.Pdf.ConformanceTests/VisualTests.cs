using Rustaveli.Pdf.ConformanceTests.Rendering;
using Rustaveli.Pdf.ConformanceTests.Specimens;
using SkiaSharp;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Every page of every specimen, rendered by PDFium, must match its approved snapshot. These pin the current output
/// so that replacing the rendering backend — or any other change — cannot alter what a reader sees unnoticed.
/// </summary>
public class VisualTests
{
    // 96 DPI keeps snapshots small while still resolving hairlines, glyph shapes and one-point offsets.
    private const float DotsPerInch = 96f;

    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void MatchesApprovedSnapshots(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf();

        List<SKBitmap> pages = PageRenderer.Render(pdf, DotsPerInch);
        try
        {
            Assert.NotEmpty(pages);

            // Every page is compared, so one run writes out every page that changed and one approval takes them all.
            List<string> mismatches = [];
            for (int index = 0; index < pages.Count; index++)
            {
                try
                {
                    SnapshotAssert.Matches($"{specimen.Name}.page{index + 1}", pages[index]);
                }
                catch (Xunit.Sdk.XunitException mismatch)
                {
                    mismatches.Add(mismatch.Message);
                }
            }

            Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
        }
        finally
        {
            foreach (SKBitmap page in pages)
                page.Dispose();
        }
    }
}
