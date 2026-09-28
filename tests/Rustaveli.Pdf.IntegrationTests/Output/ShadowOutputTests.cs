using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// How shadows come out: in the file, as an image of their ink seen through a soft mask of the blur, shared between
/// identical shadows; and on a page image, blurred by Skia itself.
/// </summary>
public class ShadowOutputTests
{
    private static Document Compose(Action<StackComposer> compose) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Margins = Sides.All(20);
            section.Body().Stack(compose);
        }));

    private static string Export(Action<StackComposer> compose) =>
        Encoding.Latin1.GetString(Compose(compose).ExportPdf(new PdfExportOptions { Compress = false }));

    [Fact]
    public void ABlurredShadowIsAnImageSeenThroughASoftMask()
    {
        string pdf = Export(stack => stack.Add().Height(40).DropShadow(Ink.Rgb(255, 0, 0), 4).Blank());

        Assert.Matches(@"/Width 1\s*/Height 1\s*/ColorSpace\s*/DeviceRGB\s*/BitsPerComponent 8\s*/Interpolate true\s*/SMask \d+ 0 R", pdf);
        Assert.Matches(@"/ColorSpace\s*/DeviceGray\s*/BitsPerComponent 8\s*/Interpolate true", pdf);
        Assert.Matches(@"/X1 Do", pdf);
        Assert.Contains("stream\n\xFF\0\0\nendstream", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void AProcessInkShadowIsInCmyk()
    {
        string pdf = Export(stack => stack.Add().Height(40).DropShadow(Ink.Cmyk(0, 0, 0, 1), 4).Blank());

        Assert.Matches(@"/Width 1\s*/Height 1\s*/ColorSpace\s*/DeviceCMYK", pdf);
    }

    [Fact]
    public void IdenticalShadowsShareOneImage()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(40).DropShadow(Ink.Rgb(255, 0, 0), 4).Blank();
            stack.Add().Height(40).DropShadow(Ink.Rgb(255, 0, 0), 4).Blank();
            stack.Add().Height(40).DropShadow(Ink.Rgb(255, 0, 0), 6).Blank();
        });

        Assert.Equal(2, Regex.Matches(pdf, @"/SMask \d+ 0 R").Count);
        Assert.Equal(3, Regex.Matches(pdf, @"/X\d Do").Count);
    }

    [Fact]
    public void ATranslucentShadowIsDrawnAtItsOpacity()
    {
        string pdf = Export(stack => stack.Add().Height(40).DropShadow(Ink.Rgb(0, 0, 0).WithOpacity(0.25f), 4).Blank());

        Assert.Matches(@"/ca 0\.25", pdf);
    }

    [Fact]
    public void ASharpShadowIsAFilledShape()
    {
        string pdf = Export(stack => stack.Add().Height(40).DropShadow(Ink.Rgb(255, 0, 0), 0, 3, 3).Blank());

        Assert.DoesNotMatch(@"/SMask", pdf);
        Assert.Matches(@"1 0 0 rg\s*3 3 160 40 re\s*f", pdf);
    }

    [Theory]
    [InlineData(-20f)]
    [InlineData(-100f)]
    public void AShadowWithNothingToShowIsNotDrawn(float spread)
    {
        // A transparent shadow, and one shrunk to nothing: the frame is only 40 points tall.
        string pdf = Export(stack =>
        {
            stack.Add().Height(40).DropShadow(Ink.Transparent, 4).Blank();
            stack.Add().Height(40).DropShadow(new Shadow(Ink.Rgb(255, 0, 0), 4, Spread: spread)).Blank();
        });

        Assert.DoesNotMatch(@"/SMask", pdf);
    }

    [Fact]
    public void APageImageShowsTheShadowSoftenedBeyondTheFrame()
    {
        Document document = Compose(stack => stack.Add().Height(40).DropShadow(Ink.Rgb(0, 0, 0), 8, 0, 20).Fill(Ink.White).Blank());

        using SKBitmap page = SKBitmap.Decode(document.ExportImages(new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Jpeg })[0]);

        // The frame is white and runs from 20 to 60 down the page; its shadow falls 20 lower, fading at its edge.
        SKColor under = page.GetPixel(100, 70);
        SKColor edge = page.GetPixel(100, 80);
        SKColor clear = page.GetPixel(100, 95);

        Assert.True(under.Red < 40, under.ToString());
        Assert.InRange(edge.Red, 80, 180);
        Assert.True(clear.Red > 240, clear.ToString());
        Assert.True(page.GetPixel(100, 40).Red > 240, page.GetPixel(100, 40).ToString());
    }

    [Fact]
    public void APageImageShowsASharpShadowWithAHardEdge()
    {
        Document document = Compose(stack => stack.Add().Height(40).DropShadow(Ink.Rgb(0, 0, 0), 0, 0, 20).Fill(Ink.White).Blank());

        using SKBitmap page = SKBitmap.Decode(document.ExportImages(new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Png })[0]);

        Assert.True(page.GetPixel(100, 78).Red < 10, page.GetPixel(100, 78).ToString());
        Assert.True(page.GetPixel(100, 82).Red > 245, page.GetPixel(100, 82).ToString());
    }
}
