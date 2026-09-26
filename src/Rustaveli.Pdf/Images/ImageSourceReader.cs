namespace Rustaveli.Pdf.Images;

/// <summary>Reads the whole of a stream into memory, refusing more than a limit.</summary>
internal static class ImageSourceReader
{
    private const int ChunkSize = 81920;

    public static byte[] ReadAll(Stream stream, long maxBytes = ImageLimits.MaxSourceBytes)
    {
        // A stream that knows its length is read straight into an array of that size, with no intermediate copy.
        // Anything else — network, compression and HTTP response streams — is gathered in chunks.
        if (stream.CanSeek)
        {
            long remaining = Math.Max(0, stream.Length - stream.Position);
            CheckSize(remaining, maxBytes);

            byte[] buffer = new byte[remaining];
            int total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                total += read;

            // A stream that ends before its reported length is taken at its word the second time.
            if (total < buffer.Length)
                Array.Resize(ref buffer, total);

            return buffer;
        }

        using MemoryStream copy = new MemoryStream();
        byte[] chunk = new byte[ChunkSize];
        int count;
        while ((count = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            CheckSize(copy.Length + count, maxBytes);
            copy.Write(chunk, 0, count);
        }

        return copy.ToArray();
    }

    private static void CheckSize(long length, long maxBytes)
    {
        if (length > maxBytes)
            throw new ImageFormatException($"The image data is larger than the {maxBytes} bytes supported.");
    }
}
