using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Real encoded images, for tests that need data a decoder will accept.
/// </summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        using SKBitmap bitmap = new SKBitmap(width, height);

        using (SKCanvas canvas = new SKCanvas(bitmap))
            canvas.Clear(new SKColor(30, 120, 200));

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// A PNG whose samples are all 128 in a linear RGB colour space, which its ICC profile says: in sRGB, the grey
    /// every screen shows, that is about 188, not 128.
    /// </summary>
    public static byte[] LinearGreyPng(int width, int height)
    {
        SKImageInfo info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque, SKColorSpace.CreateSrgbLinear());
        byte[] samples = Enumerable.Range(0, width * height * 4).Select(index => index % 4 == 3 ? (byte)255 : (byte)128).ToArray();

        using SKImage image = SKImage.FromPixelCopy(info, samples, info.RowBytes);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>The colour at the centre of an encoded image, converted to sRGB as its colour profile says.</summary>
    public static SKColor CentreInSrgb(byte[] encoded)
    {
        using SKCodec codec = SKCodec.Create(new MemoryStream(encoded)) ?? throw new InvalidOperationException("Not an image.");
        SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using SKBitmap bitmap = new SKBitmap(info);
        codec.GetPixels(info, bitmap.GetPixels());
        return bitmap.GetPixel(info.Width / 2, info.Height / 2);
    }
}
