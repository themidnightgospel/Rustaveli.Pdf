using System.Text;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Writing;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// Paths as the file writes them — segments, fill rules, stroke state and clips — and as a page image shows them.
/// </summary>
public class PathOutputTests
{
    private static readonly Ink Red = Ink.Rgb(255, 0, 0);

    private static readonly VectorPath Triangle = new VectorPath().MoveTo(10, 10).LineTo(50, 10).CurveTo(60, 20, 60, 40, 30, 50).Close();

    /// <summary>The content stream of one page, 100 points square, drawn by <paramref name="draw"/>.</summary>
    private static string Content(Action<PdfSurface> draw)
    {
        using MemoryStream stream = new MemoryStream();

        using (PdfDocumentWriter writer = new PdfDocumentWriter(stream))
        {
            using PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);
            surface.BeginPage(new Extent(100, 100));
            draw(surface);
            surface.EndPage();
            surface.Finish();
        }

        using PdfDocument parsed = PdfDocument.Open(stream.ToArray());

        return Encoding.ASCII.GetString(parsed.GetPage(1).Operations
            .Select(operation => { using MemoryStream buffer = new MemoryStream(); operation.Write(buffer); return buffer.ToArray(); })
            .SelectMany(bytes => bytes.Append((byte)'\n'))
            .ToArray());
    }

    [Fact]
    public void AFillWritesEverySegmentAndItsRule()
    {
        string nonZero = Content(surface => surface.FillPath(Triangle, Red, FillRule.NonZero));
        string evenOdd = Content(surface => surface.FillPath(Triangle, Red, FillRule.EvenOdd));

        Assert.Matches(@"10 10 m\s+50 10 l\s+60 20 60 40 30 50 c\s+h\s+f\s", nonZero);
        Assert.Matches(@"h\s+f\*\s", evenOdd);
    }

    [Fact]
    public void AStrokeWritesItsCapsJoinsAndDashesForItselfAlone()
    {
        string content = Content(surface =>
        {
            surface.StrokePath(Triangle, Red, new LineStyle(3, LineCap.Round, LineJoin.Bevel, 6, [4, 2], 1));
            surface.StrokePath(Triangle, Red, new LineStyle(2, LineCap.Square, LineJoin.Round, 10));
        });

        Assert.Matches(@"q\s+3 w\s+1 J\s+2 j\s+6 M\s+\[4 2\] 1 d\s+10 10 m[\s\S]*?S\s+Q", content);
        Assert.Matches(@"q\s+2 w\s+2 J\s+1 j\s+10 10 m[\s\S]*?S\s+Q", content);
    }

    [Fact]
    public void ADefaultStrokeSetsOnlyItsWeightAndMiterLimit()
    {
        string content = Content(surface => surface.StrokePath(Triangle, Red, new LineStyle(2)));

        Assert.Matches(@"q\s+2 w\s+4 M\s+10 10 m", content);
        Assert.DoesNotMatch(@"\sJ\s|\sj\s|\sd\s", content);
    }

    [Theory]
    [InlineData(new float[] { 0, 0 })]
    [InlineData(new float[0])]
    public void APatternOfGapsAloneOrOfNothingIsSolid(float[] dashes)
    {
        string content = Content(surface => surface.StrokePath(Triangle, Red, new LineStyle(2, Dashes: dashes)));

        Assert.DoesNotMatch(@"\sd\s", content);
    }

    [Fact]
    public void NothingIsWrittenThatCouldNotBeSeen()
    {
        string content = Content(surface =>
        {
            surface.FillPath(Triangle, Ink.Transparent, FillRule.NonZero);
            surface.FillPath(new VectorPath(), Red, FillRule.NonZero);
            surface.StrokePath(Triangle, Ink.Transparent, new LineStyle(2));
            surface.StrokePath(new VectorPath(), Red, new LineStyle(2));
            surface.StrokePath(Triangle, Red, new LineStyle(0));
        });

        Assert.DoesNotMatch(@"\s[fS]\s", content);
    }

    [Fact]
    public void AClipConfinesByItsRule()
    {
        string nonZero = Content(surface => surface.ClipPath(Triangle, FillRule.NonZero));
        string evenOdd = Content(surface => surface.ClipPath(Triangle, FillRule.EvenOdd));
        string empty = Content(surface => surface.ClipPath(new VectorPath(), FillRule.NonZero));

        Assert.Matches(@"h\s+W\s+n", nonZero);
        Assert.Matches(@"h\s+W\*\s+n", evenOdd);
        Assert.Matches(@"0 0 0 0 re\s+W\s+n", empty);
    }

    [Fact]
    public void AGeneralTransformIsConcatenated()
    {
        string content = Content(surface => surface.Concatenate(1, 0, 0.5f, 1, 10, 20));

        Assert.Matches(@"1 0 0\.5 1 10 20 cm", content);
    }

    private static SKBitmap Image(Artwork artwork)
    {
        byte[] png = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(100, 100);
            section.Body().Artwork(artwork);
        })).ExportImages(new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Png })[0];

        return SKBitmap.Decode(png);
    }

    private static bool IsRed(SKColor pixel) => pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60;

    [Fact]
    public void AnEvenOddFillLeavesItsInnerFigureEmpty()
    {
        VectorPath ring = new VectorPath().AddRectangle(10, 10, 80, 80).AddRectangle(30, 30, 40, 40);

        using SKBitmap evenOdd = Image(Artwork.Draw(100, 100, art => art.Fill(ring, Red, FillRule.EvenOdd)));
        using SKBitmap nonZero = Image(Artwork.Draw(100, 100, art => art.Fill(ring, Red)));

        Assert.True(IsRed(evenOdd.GetPixel(20, 20)));
        Assert.False(IsRed(evenOdd.GetPixel(50, 50)));
        Assert.True(IsRed(nonZero.GetPixel(50, 50)));
    }

    [Fact]
    public void AClipKeepsOnlyWhatIsInsideIt()
    {
        using SKBitmap page = Image(Artwork.Draw(100, 100, art =>
        {
            art.Clip(new VectorPath().AddCircle(50, 50, 20));
            art.Fill(new VectorPath().AddRectangle(0, 0, 100, 100), Red);
        }));

        Assert.True(IsRed(page.GetPixel(50, 50)));
        Assert.False(IsRed(page.GetPixel(10, 10)));
    }

    [Fact]
    public void AStrokeIsDrawnWithItsDashes()
    {
        using SKBitmap page = Image(Artwork.Draw(100, 100, art =>
        {
            art.Stroke(new VectorPath().MoveTo(0, 50).LineTo(100, 50), Red, new LineStyle(6, Dashes: [10], DashOffset: 0));
            art.Transform(1, 0, 0, 1, 0, 30);
            art.Stroke(new VectorPath().MoveTo(0, 50).LineTo(100, 50), Red, new LineStyle(6, LineCap.Round, LineJoin.Round));
            art.Stroke(new VectorPath().MoveTo(0, 60).LineTo(100, 60), Red, new LineStyle(6, LineCap.Square, LineJoin.Bevel));
        }));

        Assert.True(IsRed(page.GetPixel(5, 50)));
        Assert.False(IsRed(page.GetPixel(15, 50)));
        Assert.True(IsRed(page.GetPixel(25, 50)));
        Assert.True(IsRed(page.GetPixel(15, 80)));
        Assert.True(IsRed(page.GetPixel(15, 90)));
    }

    [Fact]
    public void NothingIsDrawnOnAPageImageThatCouldNotBeSeen()
    {
        using SKBitmap page = Image(Artwork.Draw(100, 100, art =>
        {
            art.Fill(new VectorPath().AddRectangle(0, 0, 100, 100), Ink.Transparent);
            art.Fill(new VectorPath(), Red);
            art.Stroke(new VectorPath().AddRectangle(0, 0, 100, 100), Ink.Transparent, new LineStyle(5));
            art.Stroke(new VectorPath(), Red, new LineStyle(5));
            art.Stroke(new VectorPath().AddRectangle(10, 10, 80, 80), Red, new LineStyle(0));
            art.Stroke(new VectorPath().AddRectangle(10, 10, 80, 80), Red, new LineStyle(2, Dashes: [0, 0]));
        }));

        Assert.False(IsRed(page.GetPixel(50, 50)));
        Assert.True(IsRed(page.GetPixel(10, 50)));
    }
}
