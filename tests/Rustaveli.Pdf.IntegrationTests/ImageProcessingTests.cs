using System.Text;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Images scaled and recompressed by Skia as they are embedded, and what that makes of the file.
/// </summary>
public class ImageProcessingTests
{
    private static string ImagePath(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    private static byte[] Export(IImage image, PdfExportOptions options, float width = 100, float height = 50) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 300);
            section.Body().Width(width).Height(height).Image(image, ImageFitting.Stretch);
        })).ExportPdf(options);

    private static IPdfImage Embedded(byte[] pdf)
    {
        using PdfDocument parsed = PdfDocument.Open(pdf);
        return Assert.Single(parsed.GetPage(1).GetImages());
    }

    [Fact]
    public void AnImageIsScaledToTheResolutionItIsShownAt()
    {
        RasterImage image = RasterImage.FromBytes(TestImages.Png(400, 200));

        IPdfImage embedded = Embedded(Export(image, new PdfExportOptions { ImageProcessor = SkiaImageProcessor.Instance, MaximumImageResolution = 72 }));

        Assert.Equal((100, 50), (embedded.WidthInSamples, embedded.HeightInSamples));
    }

    [Fact]
    public void AQualityWritesAJpeg()
    {
        RasterImage image = RasterImage.FromBytes(TestImages.Png(40, 20)).WithQuality(50);

        string pdf = Encoding.Latin1.GetString(Export(image, new PdfExportOptions { ImageProcessor = SkiaImageProcessor.Instance, Compress = false }));

        Assert.Contains("/DCTDecode", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageWithTransparencyStaysLossless()
    {
        using SKBitmap bitmap = new SKBitmap(20, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(new SKColor(255, 0, 0, 128));
        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        string pdf = Encoding.Latin1.GetString(Export(RasterImage.FromBytes(png.ToArray()).WithQuality(50), new PdfExportOptions { ImageProcessor = SkiaImageProcessor.Instance, Compress = false }));

        Assert.DoesNotContain("/DCTDecode", pdf, StringComparison.Ordinal);
        Assert.Contains("/SMask", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpaqueImageWithAnAlphaChannelIsCompressedAsAJpeg()
    {
        using SKBitmap bitmap = new SKBitmap(20, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(new SKColor(255, 0, 0, 255));
        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        string pdf = Encoding.Latin1.GetString(Export(RasterImage.FromBytes(png.ToArray()).WithQuality(50), new PdfExportOptions { ImageProcessor = SkiaImageProcessor.Instance, Compress = false }));

        Assert.Contains("/DCTDecode", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageTurnedByItsOrientationIsProcessedTheRightWayUp()
    {
        RasterImage image = RasterImage.FromFile(ImagePath("jpeg-exif-orientation6.jpg"));
        Assert.True(image.PixelHeight > image.PixelWidth || image.PixelWidth > image.PixelHeight);

        IPdfImage embedded = Embedded(Export(image.WithQuality(80), new PdfExportOptions { ImageProcessor = SkiaImageProcessor.Instance }, image.PixelWidth, image.PixelHeight));

        // Stored on its side; processed, it is stored the way it is seen.
        Assert.Equal((image.PixelWidth, image.PixelHeight), (embedded.WidthInSamples, embedded.HeightInSamples));
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopRight)]
    [InlineData(SKEncodedOrigin.BottomRight)]
    [InlineData(SKEncodedOrigin.BottomLeft)]
    [InlineData(SKEncodedOrigin.LeftTop)]
    [InlineData(SKEncodedOrigin.RightTop)]
    [InlineData(SKEncodedOrigin.RightBottom)]
    [InlineData(SKEncodedOrigin.LeftBottom)]
    [InlineData(SKEncodedOrigin.TopLeft)]
    public void EveryOrientationComesOutTheRightWayUp(SKEncodedOrigin origin)
    {
        // A 4 by 2 image, red in its stored top left corner, the rest white.
        byte[] jpeg = JpegWithOrigin(origin);
        byte[] processed = SkiaImageProcessor.Instance.Process(new ImageProcessing(jpeg, Turned(origin) ? 20 : 40, Turned(origin) ? 40 : 20, null));

        using SKBitmap upright = SKBitmap.Decode(processed);
        using SKBitmap expected = Upright(origin);

        Assert.Equal((expected.Width, expected.Height), (upright.Width, upright.Height));

        // The red square is in the same corner of both; each corner is red or white, never in between.
        foreach ((int x, int y) in new[] { (3, 3), (expected.Width - 4, 3), (3, expected.Height - 4), (expected.Width - 4, expected.Height - 4) })
            Assert.Equal(expected.GetPixel(x, y).Green < 128, upright.GetPixel(x, y).Green < 128);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(90)]
    public void AnImageKeepsItsColoursWhenItIsProcessed(int? quality)
    {
        byte[] linear = TestImages.LinearGreyPng(8, 8);
        Assert.InRange(TestImages.CentreInSrgb(linear).Red, 185, 191);

        byte[] processed = SkiaImageProcessor.Instance.Process(new ImageProcessing(linear, 4, 4, quality));

        Assert.InRange(TestImages.CentreInSrgb(processed).Red, 184, 192);
    }

    [Theory]
    [InlineData("jpeg-gray.jpg")]
    [InlineData("jpeg-cmyk-adobe.jpg")]
    [InlineData("jpeg-icc.jpg")]
    [InlineData("png-iccp.png")]
    [InlineData("basn0g16.png")]
    [InlineData("basn4a16.png")]
    [InlineData("basn6a16.png")]
    [InlineData("basn3p08.png")]
    public void EveryKindOfImageIsProcessedInSrgb(string name)
    {
        byte[] processed = SkiaImageProcessor.Instance.Process(new ImageProcessing(File.ReadAllBytes(ImagePath(name)), 8, 8, 80));

        using SKCodec codec = SKCodec.Create(new MemoryStream(processed));
        Assert.Equal((8, 8), (codec.Info.Width, codec.Info.Height));
        Assert.True(codec.Info.ColorSpace is null || codec.Info.ColorSpace.IsSrgb, $"{name} came out in another colour space.");
    }

    [Fact]
    public void AProcessorNeedsPixelsToMake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkiaImageProcessor.Instance.Process(new ImageProcessing(TestImages.Png(4, 2), 0, 1, null)));
        Assert.Throws<ArgumentException>(() => SkiaImageProcessor.Instance.Process(new ImageProcessing(new byte[] { 1, 2, 3 }, 1, 1, null)));
    }

    private static bool Turned(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// A 40 by 20 JPEG with its EXIF orientation set, red in the stored top left quarter's corner square, white else.
    /// </summary>
    private static byte[] JpegWithOrigin(SKEncodedOrigin origin)
    {
        using SKBitmap bitmap = new SKBitmap(40, 20);
        bitmap.Erase(SKColors.White);

        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
                bitmap.SetPixel(x, y, SKColors.Red);
        }

        using SKData plain = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
        byte[] data = plain.ToArray();

        // An APP1 Exif segment holding one orientation entry, straight after the start of image.
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01, 0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)origin, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        return [.. data.Take(2), .. exif, .. data.Skip(2)];
    }

    /// <summary>What the image with that origin looks like, drawn the long way.</summary>
    private static SKBitmap Upright(SKEncodedOrigin origin)
    {
        bool turned = Turned(origin);
        SKBitmap bitmap = new SKBitmap(turned ? 20 : 40, turned ? 40 : 20);
        bitmap.Erase(SKColors.White);

        // Where the stored top left square lands once the image is the right way up.
        bool right = origin is SKEncodedOrigin.TopRight or SKEncodedOrigin.BottomRight or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom;
        bool bottom = origin is SKEncodedOrigin.BottomRight or SKEncodedOrigin.BottomLeft or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int left = right ? bitmap.Width - 10 : 0;
        int top = bottom ? bitmap.Height - 10 : 0;

        for (int y = top; y < top + 10; y++)
        {
            for (int x = left; x < left + 10; x++)
                bitmap.SetPixel(x, y, SKColors.Red);
        }

        return bitmap;
    }
}
