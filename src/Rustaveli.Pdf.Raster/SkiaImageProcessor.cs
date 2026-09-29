using SkiaSharp;

namespace Rustaveli.Pdf;

/// <summary>
/// Re-encodes images for PDF export with Skia: turned the right way up, scaled with a cubic filter to the pixels
/// asked for, and written as JPEG at the quality asked for — or as PNG where the image has transparency or no quality
/// is asked.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>The processor; it keeps no state, so one serves every export.</summary>
    public static SkiaImageProcessor Instance { get; } = new SkiaImageProcessor();

    public byte[] Process(ImageProcessing request)
    {
        if (request.PixelWidth < 1 || request.PixelHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "An image is at least one pixel each way.");

        using SKData source = SKData.CreateCopy(request.Source.ToArray());
        using SKCodec codec = SKCodec.Create(source) ?? throw new ArgumentException("The image could not be decoded.", nameof(request));

        // Converted to sRGB as it is decoded, and kept in sRGB to the end: re-encoded, the image carries no profile of
        // its own, so its samples must already be in the colours the PDF takes them to be in.
        using SKBitmap stored = SKBitmap.Decode(codec, codec.Info.WithColorSpace(SKColorSpace.CreateSrgb())) ?? throw new ArgumentException("The image could not be decoded.", nameof(request));
        using SKBitmap upright = Upright(stored, codec.EncodedOrigin);
        using SKBitmap scaled = upright.Resize(
            new SKImageInfo(request.PixelWidth, request.PixelHeight, upright.ColorType, upright.AlphaType, upright.ColorSpace),
            new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? throw new InvalidOperationException("The image could not be scaled.");

        bool lossless = request.Quality is null || HasTransparency(scaled);
        using SKData encoded = lossless
            ? scaled.Encode(SKEncodedImageFormat.Png, 100)
            : scaled.Encode(SKEncodedImageFormat.Jpeg, request.Quality!.Value);

        return encoded.ToArray();
    }

    /// <summary>The pixels as they are meant to be seen, after the turn or mirroring the EXIF origin asks for.</summary>
    private static SKBitmap Upright(SKBitmap stored, SKEncodedOrigin origin)
    {
        int width = stored.Width;
        int height = stored.Height;
        bool turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // Each maps a stored pixel (x, y) to where it is seen, as x' = a·x + b·y + c and y' = d·x + e·y + f.
        SKMatrix matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        SKBitmap upright = new SKBitmap(new SKImageInfo(turned ? height : width, turned ? width : height, stored.ColorType, stored.AlphaType, stored.ColorSpace));

        using SKCanvas canvas = new SKCanvas(upright);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(in matrix);
        using SKImage image = SKImage.FromBitmap(stored);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);

        return upright;
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
            return false;

        // The alpha of every pixel, copied out in one call and read as bytes: asking for each pixel in turn is a
        // native call apiece, 24 million of them for a photograph of 24 megapixels.
        using SKBitmap alpha = bitmap.Copy(SKColorType.Alpha8) ?? throw new InvalidOperationException("The image's transparency could not be read.");
        ReadOnlySpan<byte> samples = alpha.GetPixelSpan();

        for (int y = 0; y < alpha.Height; y++)
        {
            foreach (byte sample in samples.Slice(y * alpha.RowBytes, alpha.Width))
            {
                if (sample < 255)
                    return true;
            }
        }

        return false;
    }
}
