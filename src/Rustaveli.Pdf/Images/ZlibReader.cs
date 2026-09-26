using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Decompresses a zlib stream (RFC 1950): checks the two-byte header, inflates the deflate data with the runtime's
/// <see cref="DeflateStream"/>, and verifies the trailing Adler-32 checksum over everything it produced.
/// </summary>
/// <remarks>
/// System.IO.Compression's ZLibStream would do the framing, but it does not exist on netstandard2.0 and does not
/// verify the checksum on every runtime; doing it here gives both targets the same, strict behaviour. The
/// checksum matters: deflate data can be damaged in ways that still inflate, silently, to the wrong pixels.
/// </remarks>
internal sealed class ZlibReader : IDisposable
{
    private const int HeaderLength = 2;
    private const int ChecksumLength = 4;

    private readonly DeflateStream _inflater;
    private readonly uint _expectedChecksum;
    private uint _checksum = Adler32.Initial;

    public ZlibReader(ReadOnlyMemory<byte> zlib)
    {
        ReadOnlySpan<byte> span = zlib.Span;
        if (span.Length < HeaderLength + ChecksumLength)
            throw new ImageFormatException("The compressed data is too short to be a zlib stream.");

        CheckHeader(span[0], span[1]);
        _expectedChecksum = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(span.Length - ChecksumLength));

        ArraySegment<byte> deflate = AsArraySegment(zlib.Slice(HeaderLength, zlib.Length - HeaderLength - ChecksumLength));
        MemoryStream input = new MemoryStream(deflate.Array!, deflate.Offset, deflate.Count, writable: false);
        _inflater = new DeflateStream(input, CompressionMode.Decompress);
    }

    /// <summary>
    /// Reads up to <paramref name="count"/> decompressed bytes, returning 0 only at the end of the stream.
    /// </summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        int read;
        try
        {
            read = _inflater.Read(buffer, offset, count);
        }
        catch (InvalidDataException exception)
        {
            throw new ImageFormatException("The compressed data is corrupt.", exception);
        }

        _checksum = Adler32.Append(_checksum, buffer.AsSpan(offset, read));
        return read;
    }

    /// <summary>Reads exactly <paramref name="count"/> decompressed bytes, or throws if the stream ends first.</summary>
    public void ReadExactly(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = Read(buffer, offset, count);
            if (read == 0)
                throw new ImageFormatException("The compressed data ends before all of the image has been read.");

            offset += read;
            count -= read;
        }
    }

    /// <summary>
    /// Consumes anything left in the stream and verifies the checksum, which covers every decompressed byte.
    /// </summary>
    /// <remarks>
    /// Data beyond what the caller needed is tolerated, as libpng tolerates it, but it must still be read: the
    /// checksum cannot be verified otherwise. The work is bounded by deflate's maximum expansion of the input.
    /// </remarks>
    public void Finish()
    {
        byte[] scratch = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (Read(scratch, 0, scratch.Length) > 0)
            {
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }

        if (_checksum != _expectedChecksum)
            throw new ImageFormatException("The compressed data fails its Adler-32 checksum.");
    }

    public void Dispose() => _inflater.Dispose();

    /// <summary>Inflates a whole zlib stream, refusing to produce more than <paramref name="maxLength"/> bytes.</summary>
    public static byte[] InflateAll(ReadOnlyMemory<byte> zlib, int maxLength)
    {
        using ZlibReader reader = new ZlibReader(zlib);
        using MemoryStream output = new MemoryStream();
        byte[] buffer = new byte[4096];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > maxLength)
                throw new ImageFormatException($"The compressed data expands beyond {maxLength} bytes.");

            output.Write(buffer, 0, read);
        }

        reader.Finish();
        return output.ToArray();
    }

    private static void CheckHeader(byte method, byte flags)
    {
        // Compression method 8 (deflate) with a window of at most 32 KiB, a header that is a multiple of 31 as its
        // check bits require, and no preset dictionary, which neither PNG nor PDF allows.
        if ((method & 0x0F) != 8 || (method >> 4) > 7 || ((method << 8) | flags) % 31 != 0 || (flags & 0x20) != 0)
            throw new ImageFormatException("The compressed data does not start with a valid zlib header.");
    }

    private static ArraySegment<byte> AsArraySegment(ReadOnlyMemory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment)
            ? segment
            : new ArraySegment<byte>(memory.ToArray());
}
