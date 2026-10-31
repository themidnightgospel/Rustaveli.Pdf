namespace Rustaveli.Pdf.Images;

/// <summary>Reads the whole of a stream into memory, refusing more than a limit.</summary>
internal static class ImageSourceReader
{
    public static byte[] ReadAll(Stream stream, long maxBytes = ImageLimits.MaxSourceBytes) =>
        StreamBytes.ReadAll(stream, length => CheckSize(length, maxBytes));

    private static void CheckSize(long length, long maxBytes)
    {
        if (length > maxBytes)
            throw new ImageFormatException($"The image data is larger than the {maxBytes} bytes supported.");
    }
}
