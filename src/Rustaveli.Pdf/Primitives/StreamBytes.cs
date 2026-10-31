namespace Rustaveli.Pdf;

/// <summary>Reads the rest of a stream into memory, as an array of exactly the bytes read.</summary>
internal static class StreamBytes
{
    private const int ChunkSize = 81920;

    /// <summary>Everything from the stream's position to its end.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="checkLength">
    /// Given each length the data is about to reach, before it is held, to throw when that is more than the caller
    /// accepts; null to accept any length.
    /// </param>
    public static byte[] ReadAll(Stream stream, Action<long>? checkLength = null)
    {
        // A stream that knows its length is read straight into an array of that size, with no intermediate copy.
        // Anything else — network, compression and HTTP response streams — is gathered in chunks.
        if (stream.CanSeek)
        {
            long remaining = Math.Max(0, stream.Length - stream.Position);
            checkLength?.Invoke(remaining);

            byte[] buffer = new byte[remaining];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }

            // A stream that ends before its reported length is taken at its word the second time.
            if (total < buffer.Length)
                Array.Resize(ref buffer, total);

            return buffer;
        }

        using MemoryStream copy = new MemoryStream();
        byte[] chunk = new byte[ChunkSize];
        while (true)
        {
            int count = stream.Read(chunk, 0, chunk.Length);
            if (count == 0)
                break;

            checkLength?.Invoke(copy.Length + count);
            copy.Write(chunk, 0, count);
        }

        return copy.ToArray();
    }
}
