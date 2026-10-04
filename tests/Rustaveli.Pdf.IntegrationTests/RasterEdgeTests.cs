using System.Runtime.InteropServices;
using System.Text;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Raster;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The Skia surface and the targets it draws on at their edges: drawing that shows nothing is left out, misuse is
/// refused, images stand upright in every orientation, and each export honours its options.
/// </summary>
public class RasterEdgeTests
{
    private static readonly TypefaceLibrary Latin = TestFonts.NewLibrary(includeInstalled: false);

    private static Document Build(string text) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(100, 50);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
        section.Body().Text(text);
    }));

    /// <summary>One page of SVG with whatever <paramref name="draw"/> puts on it.</summary>
    private static string Svg(Action<SkiaRasterSurface> draw, TypefaceLibrary? typefaces = null)
    {
        using SkiaRasterSurface surface = new SkiaRasterSurface((typefaces ?? Latin).Shaper, new SvgPageTarget());
        surface.BeginPage(new Extent(100, 100));
        draw(surface);
        surface.EndPage();
        return Encoding.UTF8.GetString(surface.Pages[0]);
    }

    [Fact]
    public void AFaceIsLoadedIntoSkiaOnceForEveryExport()
    {
        OpenTypeFont face = OpenTypeFont.LoadFile(TestFonts.PathOf("NotoSans-Regular.ttf"));

        Assert.Same(SkiaRasterSurface.TypefaceFor(face), SkiaRasterSurface.TypefaceFor(face));
        Assert.NotSame(SkiaRasterSurface.TypefaceFor(face), SkiaRasterSurface.TypefaceFor(OpenTypeFont.LoadFile(TestFonts.PathOf("NotoSans-Regular.ttf"))));
    }

    [Fact]
    public void DrawingThatShowsNothingLeavesNothing()
    {
        VectorPath square = new VectorPath().AddRectangle(10, 10, 20, 20);
        Offset from = new Offset(10, 10);
        Offset to = new Offset(90, 10);
        string blank = Svg(_ => { });

        string drawn = Svg(surface =>
        {
            surface.FillPath(square, Ink.Transparent, FillRule.NonZero);
            surface.FillPath(new VectorPath(), Ink.Black, FillRule.NonZero);
            surface.StrokePath(square, Ink.Transparent, new LineStyle(1));
            surface.StrokePath(new VectorPath(), Ink.Black, new LineStyle(1));
            surface.StrokePath(square, Ink.Black, new LineStyle(0));
            surface.FillRectangle(from, new Extent(10, 10), Ink.Transparent);
            surface.FillRectangle(from, new Extent(0, 10), Ink.Black);
            surface.FillRectangle(from, new Extent(10, 0), Ink.Black);
            surface.DrawRoundedRectangle(from, new Extent(10, 10), Corners.All(2), Ink.Transparent);
            surface.DrawRoundedRectangle(from, new Extent(0, 10), Corners.All(2), Ink.Black);
            surface.DrawRoundedRectangle(from, new Extent(10, 0), Corners.All(2), Ink.Black);
            surface.DrawLine(from, to, 1, Ink.Transparent);
            surface.DrawLine(from, to, 0, Ink.Black);
            surface.DrawDashedLine(from, to, 1, Ink.Transparent, [2, 2]);
            surface.DrawDashedLine(from, to, 0, Ink.Black, [2, 2]);
            surface.ShowText(string.Empty, from, TypeStyle.Default.WithTypeface(TestFonts.Sans), ReadingDirection.LeftToRight);
            surface.ShowText("Hidden", from, TypeStyle.Default.WithTypeface(TestFonts.Sans).WithInk(Ink.Transparent), ReadingDirection.LeftToRight);
            surface.DrawShadow(from, new Extent(10, 10), Corners.Zero, new Shadow(Ink.Transparent, 2));
            surface.DrawShadow(from, Extent.Zero, Corners.Zero, new Shadow(Ink.Black, 0));
        });

        Assert.Equal(blank, drawn);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    public void AFillThinnerThanAPixelIsDrawnAsOneDarkRowAsPdfViewersDrawIt(float thickness)
    {
        // A 0.25-point rule is a third of a pixel at 96 dots an inch. Drawn as its share of a pixel it is a faint grey
        // smear across two rows; PDF viewers draw it one crisp pixel thick, and a page image should look the same.
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(100, 50);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(10.4f);
                stack.Add().Height(thickness).Fill(Ink.Black);
            });
        }));

        using SKBitmap image = SKBitmap.Decode(document.ExportImages(new ImageExportOptions { Resolution = 96, Format = PageImageFormat.Jpeg, Quality = 100 })[0]);
        List<int> dark = Enumerable.Range(0, image.Height).Where(row => image.GetPixel(image.Width / 2, row).Red < 96).ToList();

        Assert.Single(dark);
    }

    [Fact]
    public void WhatShowsIsDrawn()
    {
        string blank = Svg(_ => { });

        Assert.NotEqual(blank, Svg(surface => surface.FillRectangle(new Offset(10, 10), new Extent(10, 10), Ink.Black)));
        Assert.NotEqual(blank, Svg(surface => surface.DrawLine(new Offset(10, 10), new Offset(90, 10), 1, Ink.Black)));
        Assert.NotEqual(blank, Svg(surface => surface.DrawShadow(new Offset(10, 10), new Extent(10, 10), Corners.Zero, new Shadow(Ink.Black, 2))));
    }

    [Fact]
    public void TextInTwoTypefacesIsDrawnAsOneOutlinePerGlyph()
    {
        TypefaceLibrary typefaces = TestFonts.NewLibrary(includeInstalled: false);
        typefaces.RegisterFile(TestFonts.PathOf("NotoSansGeorgian-Regular.ttf"));
        typefaces.Fallbacks = ["Noto Sans Georgian"];

        string svg = Svg(surface => surface.ShowText("Hაb", new Offset(10, 50), TypeStyle.Default.WithTypeface(TestFonts.Sans), ReadingDirection.LeftToRight), typefaces);

        // The Latin letters are set in one face and the Georgian in another, each glyph once, in the order written.
        List<float> across = System.Text.RegularExpressions.Regex.Matches(svg, "<path[^>]*transform=\"translate\\(([0-9.]+)")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(svg, "<path").Count);
        Assert.Equal(across.OrderBy(x => x), across);
    }

    [Fact]
    public void MisuseOfTheSurfaceIsRefused()
    {
        using SkiaRasterSurface surface = new SkiaRasterSurface(Latin.Shaper, new ImageExportOptions());

        Assert.Equal("No page is open. BeginPage must be called before drawing.", Assert.Throws<InvalidOperationException>(surface.EndPage).Message);
        Assert.Equal(
            "No page is open. BeginPage must be called before drawing.",
            Assert.Throws<InvalidOperationException>(() => surface.FillRectangle(Offset.Zero, new Extent(1, 1), Ink.Black)).Message);

        surface.BeginPage(new Extent(10, 10));

        Assert.Equal(
            "A page is already open. EndPage must be called before the next BeginPage.",
            Assert.Throws<InvalidOperationException>(() => surface.BeginPage(new Extent(10, 10))).Message);
    }

    public static TheoryData<string> Targets()
    {
        TheoryData<string> targets = new TheoryData<string> { "raster", "svg" };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            targets.Add("xps");

        return targets;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void MisuseOfATargetIsRefused(string kind)
    {
        using ISkiaPageTarget target = kind switch
        {
            "raster" => new RasterPageTarget(new ImageExportOptions()),
            "svg" => new SvgPageTarget(),
            _ => new XpsPageTarget(),
        };

        Assert.Equal("No page is open. BeginPage must be called before drawing.", Assert.Throws<InvalidOperationException>(() => target.End()).Message);

        target.Begin(new Extent(10, 10), out _);

        Assert.Equal(
            "A page is already open. EndPage must be called before the next BeginPage.",
            Assert.Throws<InvalidOperationException>(() => target.Begin(new Extent(10, 10), out _)).Message);
    }

    [Theory]
    [InlineData((int)ExifOrientation.Normal, 0, 0)]
    [InlineData((int)ExifOrientation.FlipHorizontal, 40, 0)]
    [InlineData((int)ExifOrientation.Rotate180, 40, 20)]
    [InlineData((int)ExifOrientation.FlipVertical, 0, 20)]
    [InlineData((int)ExifOrientation.Transpose, 0, 0)]
    [InlineData((int)ExifOrientation.Rotate90, 40, 0)]
    [InlineData((int)ExifOrientation.Transverse, 40, 20)]
    [InlineData((int)ExifOrientation.Rotate270, 0, 20)]
    public void AnImageStandsUprightInItsBoxWhateverItsOrientation(int tag, float originX, float originY)
    {
        ExifOrientation orientation = (ExifOrientation)tag;

        // Turned a quarter, the stored image is as tall as the box is wide.
        bool quarter = orientation >= ExifOrientation.Transpose;
        (float storedWidth, float storedHeight) = quarter ? (2f, 4f) : (4f, 2f);

        SKMatrix placement = SkiaRasterSurface.Placement(orientation, storedWidth, storedHeight, 40, 20);

        SKPoint[] corners =
        [
            placement.MapPoint(0, 0),
            placement.MapPoint(storedWidth, 0),
            placement.MapPoint(0, storedHeight),
            placement.MapPoint(storedWidth, storedHeight),
        ];

        Assert.Equal(new SKPoint(originX, originY), corners[0]);
        Assert.Equal(
            new[] { (0f, 0f), (0f, 20f), (40f, 0f), (40f, 20f) },
            corners.Select(corner => (Round(corner.X), Round(corner.Y))).OrderBy(corner => corner));

        static float Round(float value) => (float)Math.Round(value, 3);
    }

    [Fact]
    public void EveryExportSetsTextNoTypefaceLacksWhenEveryGlyphIsRequired()
    {
        Assert.NotEmpty(Build("Hello").ExportImages(new ImageExportOptions { Typefaces = Latin, RequireEveryGlyph = true, Resolution = 36 }));
        Assert.NotEmpty(Build("Hello").ExportSvg(new VectorExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));
        Assert.NotEmpty(Build("世").ExportSvg(new VectorExportOptions { Typefaces = Latin }));

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        Assert.NotEmpty(Build("Hello").ExportXps(new VectorExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));
        Assert.NotEmpty(Build("世").ExportXps(new VectorExportOptions { Typefaces = Latin }));
    }

    [Fact]
    public void AnXpsFileWithNoPathIsRefusedBeforeAnythingIsDrawn()
    {
        int requests = 0;
        Document document = Document.Compose(composition => composition.Section(section =>
            section.Body().Image(request =>
            {
                requests++;
                return TestImages.Png(request.PixelWidth, request.PixelHeight);
            })));

        Assert.ThrowsAny<ArgumentException>(() => document.ExportXps(string.Empty));
        Assert.Equal(0, requests);
    }

    [Fact]
    public void XpsCarriesTheFontsItsTextIsSetIn()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string names = Encoding.ASCII.GetString(Build("Hello").ExportXps(new VectorExportOptions { Typefaces = Latin }));

        Assert.Contains(".odttf", names, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefusalsSayWhatWasWrong()
    {
        byte[] png = TestImages.Png(4, 4);

        Assert.Contains(
            "at least one pixel",
            Assert.Throws<ArgumentOutOfRangeException>(() => SkiaImageProcessor.Instance.Process(new ImageProcessing(png, 0, 1, null))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "could not be decoded",
            Assert.Throws<ArgumentException>(() => SkiaImageProcessor.Instance.Process(new ImageProcessing(new byte[] { 1, 2, 3 }, 1, 1, null))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "pixels per inch",
            Assert.Throws<ArgumentOutOfRangeException>(() => new VectorExportOptions { ImageResolution = 0 }).Message,
            StringComparison.Ordinal);
    }
}
