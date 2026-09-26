using Rustaveli.Pdf.ConformanceTests.Rendering;
using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.Skia;
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

    // Skipped until decorators fill the space their parent allots rather than their child's natural size: approving
    // snapshots of today's output would pin table stripes that stop at the text and misplaced right-to-left boxes.
    [Theory(Skip = "Awaiting the fix to how decorators size what they paint; see the phase 0 pull request.")]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void MatchesApprovedSnapshots(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().GeneratePdf();

        List<SKBitmap> pages = PageRenderer.Render(pdf, DotsPerInch);
        try
        {
            Assert.NotEmpty(pages);
            for (int index = 0; index < pages.Count; index++)
                SnapshotAssert.Matches($"{specimen.Name}.page{index + 1}", pages[index]);
        }
        finally
        {
            foreach (SKBitmap page in pages)
                page.Dispose();
        }
    }
}
