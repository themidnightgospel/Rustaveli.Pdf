using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Every document of resvg's SVG test suite drawn on a page passes qpdf's structural check: whatever odd or broken
/// SVG the reader takes in, the PDF it is drawn into is sound.
/// </summary>
public class SvgCorpusTests
{
    public static TheoryData<string> Documents => SvgCorpus.Documents;

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryDocumentDrawsASoundPdf(string document)
    {
        TestFonts.EnsureRegistered();
        Artwork artwork;

        try
        {
            artwork = Artwork.FromSvgFile(SvgCorpus.PathOf(document));
        }
        catch (FormatException) when (SvgCorpus.Refusable.Contains(document))
        {
            return;
        }

        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Margins = Sides.All(0);
            section.Body().Artwork(artwork, ImageFitting.Proportionally);
        })).ExportPdf();

        Assert.Contains("No syntax or stream encoding errors found", Qpdf.Check(pdf), StringComparison.Ordinal);
    }
}
