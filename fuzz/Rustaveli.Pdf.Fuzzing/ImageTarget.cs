using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// A JPEG or PNG file read and encoded as it would be embedded in a PDF: its header and chunks parsed, its pixels
/// decoded where they must be and compressed again. The only acceptable failure is <see cref="ImageFormatException"/>.
/// </summary>
internal static class ImageTarget
{
    public static void Run(ReadOnlySpan<byte> input)
    {
        try
        {
            _ = RasterImage.Load(input.ToArray()).Encode();
        }
        catch (ImageFormatException)
        {
            // Not an image this library can read: refused, as it should be.
        }
    }
}
