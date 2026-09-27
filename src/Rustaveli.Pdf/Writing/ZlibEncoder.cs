using System.Buffers.Binary;
using System.IO.Compression;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Compresses data into the zlib format (RFC 1950) that PDF's FlateDecode filter reads: a two-byte header, a raw
/// DEFLATE stream, and the Adler-32 checksum of the uncompressed data.
/// </summary>
/// <remarks>
/// <c>DeflateStream</c> produces only the middle part, and netstandard2.0 has no <c>ZLibStream</c>, so the framing
/// is written here — the same code on both targets, and so the same bytes from the same runtime.
/// </remarks>
internal static class ZlibEncoder
{
    /// <summary>
    /// Deflate with a 32 KiB window. Every valid header is a multiple of 31 when read as a big-endian 16-bit number,
    /// which fixes the second byte for each compression-level hint.
    /// </summary>
    private const byte Method = 0x78;

    private const byte FastestFlags = 0x01;

    private const byte DefaultFlags = 0x9C;

    /// <summary>Appends the zlib encoding of <paramref name="data"/> to <paramref name="destination"/>.</summary>
    public static void Compress(ReadOnlySpan<byte> data, CompressionLevel level, PdfByteWriter destination)
    {
        destination.WriteByte(Method);
        destination.WriteByte(level == CompressionLevel.Fastest ? FastestFlags : DefaultFlags);

        using (PdfByteWriterStream sink = new PdfByteWriterStream(destination))
        using (DeflateStream deflate = new DeflateStream(sink, level, leaveOpen: true))
        {
            deflate.Write(data);
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination.GetSpan(4), Adler32.Compute(data));
        destination.Advance(4);
    }
}
