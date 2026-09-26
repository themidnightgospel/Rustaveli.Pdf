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
}
