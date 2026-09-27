using Rustaveli.Pdf.ConformanceTests.Rendering;
using Rustaveli.Pdf.Images;
using SkiaSharp;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.ConformanceTests.Images;

/// <summary>
/// Each fixture embedded as its <see cref="EncodedImage"/> says, rendered by PDFium, compared with SkiaSharp's
/// decoding of the original file over the same white page. This is what shows that the model is read the way it is
/// meant — predictor parameters, 16-bit samples, palettes, colour keys, soft masks, ICC profiles, inverted CMYK —
/// by a real PDF reader, not only by this repository's own tests.
/// </summary>
public class PdfiumImageTests(ITestOutputHelper output)
{
    // Each image pixel becomes a block of Scale × Scale device pixels, sampled at its centre, so that image
    // smoothing at the block edges cannot reach the sample.
    private const int Scale = 8;

    public static TheoryData<string> Pngs => new TheoryData<string>(Fixtures(".png"));

    public static TheoryData<string> Jpegs => new TheoryData<string>(Fixtures(".jpg").Where(name => !name.Contains("cmyk")));

    private static IEnumerable<string> Fixtures(string extension) =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "assets", "images"), "*" + extension)
            .Select(path => Path.GetFileName(path))
            .Where(name => !name.StartsWith("x", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "images", name));

    /// <summary>Skia's decoding of the file, composited over white, as RGB.</summary>
    private static byte[] Expected(byte[] file)
    {
        using SKData data = SKData.CreateCopy(file);
        using SKCodec codec = SKCodec.Create(data);
        SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKBitmap bitmap = new SKBitmap(info);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels()));

        byte[] rgba = bitmap.Bytes;
        byte[] rgb = new byte[info.Width * info.Height * 3];
        for (int pixel = 0; pixel < info.Width * info.Height; pixel++)
        {
            int alpha = rgba[(pixel * 4) + 3];
            for (int channel = 0; channel < 3; channel++)
                rgb[(pixel * 3) + channel] = (byte)(((rgba[(pixel * 4) + channel] * alpha) + (255 * (255 - alpha)) + 127) / 255);
        }

        return rgb;
    }

    /// <summary>PDFium's rendering of the embedded image, one sample per image pixel, as RGB.</summary>
    private static byte[] Rendered(EncodedImage image)
    {
        List<SKBitmap> pages = PageRenderer.Render(SingleImagePdf.Build(image), 72f * Scale);
        try
        {
            SKBitmap page = Assert.Single(pages);
            Assert.Equal(image.Width * Scale, page.Width);
            Assert.Equal(image.Height * Scale, page.Height);

            byte[] rgb = new byte[image.Width * image.Height * 3];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    SKColor color = page.GetPixel((x * Scale) + (Scale / 2), (y * Scale) + (Scale / 2));
                    int offset = ((y * image.Width) + x) * 3;
                    rgb[offset] = color.Red;
                    rgb[offset + 1] = color.Green;
                    rgb[offset + 2] = color.Blue;
                }
            }

            return rgb;
        }
        finally
        {
            foreach (SKBitmap page in pages)
                page.Dispose();
        }
    }

    private (int Maximum, double Mean) Difference(string name, byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        int maximum = 0;
        long total = 0;
        for (int index = 0; index < expected.Length; index++)
        {
            int difference = Math.Abs(expected[index] - actual[index]);
            maximum = Math.Max(maximum, difference);
            total += difference;
        }

        double mean = (double)total / expected.Length;
        output.WriteLine($"{name}: maximum difference {maximum}, mean {mean:F3}");
        return (maximum, mean);
    }

    [Theory]
    [MemberData(nameof(Pngs))]
    public void RendersEveryPngAsSkiaDecodesIt(string name)
    {
        byte[] file = Fixture(name);

        (int maximum, _) = Difference(name, Expected(file), Rendered(RasterImage.FromBytes(file).Encode()));

        // One level: PDFium and Skia round differently when narrowing 16-bit samples and when blending alpha.
        Assert.True(maximum <= 1, $"{name} differs by up to {maximum}.");
    }

    [Theory]
    [MemberData(nameof(Jpegs))]
    public void RendersEveryJpegAsSkiaDecodesIt(string name)
    {
        byte[] file = Fixture(name);

        (int maximum, double mean) = Difference(name, Expected(file), Rendered(RasterImage.FromBytes(file).Encode()));

        // Both decode with libjpeg-turbo, so the passthrough JPEG should render identically.
        Assert.True(maximum <= 1, $"{name} differs by up to {maximum}, {mean:F3} on average.");
    }

    [Fact]
    public void RendersAnRgbJpegWithoutAnAdobeSegment()
    {
        // The Adobe segment is what tells readers the fixture is RGB; with it removed, only the component
        // identifiers and the model's /ColorTransform 0 say so.
        byte[] adobe = Fixture("jpeg-rgb-adobe.jpg");
        int length = (adobe[4] << 8) | adobe[5];
        byte[] file = [.. adobe.AsSpan(0, 2), .. adobe.AsSpan(2 + 2 + length)];
        EncodedImage image = RasterImage.FromBytes(file).Encode();

        (int maximum, double mean) = Difference("rgb without Adobe", Expected(file), Rendered(image));

        Assert.Equal(0, image.ColorTransform);
        Assert.True(maximum <= 1, $"The RGB JPEG differs by up to {maximum}, {mean:F3} on average.");
    }

    [Fact]
    public void RendersAdobeCmykUpright()
    {
        byte[] file = Fixture("jpeg-cmyk-adobe.jpg");
        EncodedImage image = RasterImage.FromBytes(file).Encode();
        EncodedImage uninverted = new EncodedImage(image.Width, image.Height, image.ColorSpace, image.BitsPerComponent,
            image.Filter, image.Data);
        byte[] expected = Expected(file);

        (_, double withDecode) = Difference("with /Decode", expected, Rendered(image));
        (_, double withoutDecode) = Difference("without /Decode", expected, Rendered(uninverted));

        // PDFium and Skia convert CMYK to RGB by different formulas, so even the right rendering differs somewhat
        // (about 23 levels on average); the wrong one is a negative (about 109).
        Assert.True(withDecode < 40, $"Inverted as the model says, the image differs by {withDecode:F3} on average.");
        Assert.True(withoutDecode > 80, $"Left uninverted, the image differs by only {withoutDecode:F3} on average.");
    }
}
