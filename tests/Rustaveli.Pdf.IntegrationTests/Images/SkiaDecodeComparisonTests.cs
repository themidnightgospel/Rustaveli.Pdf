using Rustaveli.Pdf.Images;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Images;

/// <summary>
/// Every PNG fixture, as it would be embedded, compared pixel by pixel with SkiaSharp's decoding of the same file —
/// an independent PNG decoder (libpng lineage) that shares no code with this library.
/// </summary>
public class SkiaDecodeComparisonTests
{
    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    public static TheoryData<string> Pngs => new TheoryData<string>(Fixtures(".png"));

    public static TheoryData<string> Jpegs => new TheoryData<string>(Fixtures(".jpg"));

    private static IEnumerable<string> Fixtures(string extension) =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "assets", "images"), "*" + extension)
            .Select(path => Path.GetFileName(path))
            .Where(name => !name.StartsWith("x", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>Decodes with Skia into unpremultiplied RGBA, with no colour management.</summary>
    private static (int Width, int Height, byte[] Rgba) DecodeWithSkia(byte[] file)
    {
        using SKData data = SKData.CreateCopy(file);
        using SKCodec codec = SKCodec.Create(data);
        SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKBitmap bitmap = new SKBitmap(info);

        Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels()));
        return (info.Width, info.Height, bitmap.Bytes);
    }

    // Skia narrows 16-bit samples by rounding, the readers here by truncating: one level apart at most.
    private static void AssertSamePixels(string name, byte[] expected, byte[] actual, int width, int height, int bitDepth)
    {
        int tolerance = bitDepth == 16 ? 1 : 0;
        Assert.Equal(width * height * 4, actual.Length);

        for (int pixel = 0; pixel < width * height; pixel++)
        {
            int offset = pixel * 4;
            string where = $"{name} pixel ({pixel % width}, {pixel / width})";
            Assert.True(
                Math.Abs(expected[offset + 3] - actual[offset + 3]) <= tolerance,
                $"{where}: alpha {actual[offset + 3]}, Skia {expected[offset + 3]}");

            // A fully transparent pixel has no colour to compare.
            if (expected[offset + 3] == 0)
                continue;

            for (int channel = 0; channel < 3; channel++)
            {
                Assert.True(
                    Math.Abs(expected[offset + channel] - actual[offset + channel]) <= tolerance,
                    $"{where}: channel {channel} is {actual[offset + channel]}, Skia {expected[offset + channel]}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Pngs))]
    public void DecodesEveryPngToThePixelsSkiaDecodes(string name)
    {
        byte[] file = File.ReadAllBytes(PathOf(name));
        (int width, int height, byte[] expected) = DecodeWithSkia(file);
        PngFile png = PngParser.Parse(file);

        byte[] actual = DecodedPixels.Rgba(png);

        AssertSamePixels(name, expected, actual, width, height, png.Header.BitDepth);
    }

    [Theory]
    [MemberData(nameof(Pngs))]
    public void EmbedsEveryPngWithThePixelsSkiaDecodes(string name)
    {
        byte[] file = File.ReadAllBytes(PathOf(name));
        (int width, int height, byte[] expected) = DecodeWithSkia(file);

        RasterImage image = RasterImage.FromBytes(file);
        byte[] actual = EmbeddedPixels.Rgba(image.Encode());

        Assert.Equal(width, image.PixelWidth);
        Assert.Equal(height, image.PixelHeight);
        AssertSamePixels(name, expected, actual, width, height, PngParser.Parse(file).Header.BitDepth);
    }

    [Theory]
    [MemberData(nameof(Jpegs))]
    public void ReadsTheSizeSkiaDecodesFromEveryJpeg(string name)
    {
        byte[] file = File.ReadAllBytes(PathOf(name));
        (int width, int height, _) = DecodeWithSkia(file);

        RasterImage image = RasterImage.FromBytes(file);

        // Skia reports the pixels as stored; the public size is upright, with any quarter turn applied.
        Assert.Equal(width, image.StoredWidth);
        Assert.Equal(height, image.StoredHeight);
        Assert.Equal(file, image.Encode().Data.ToArray());
    }
}
