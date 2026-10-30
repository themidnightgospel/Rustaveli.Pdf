using System.Buffers.Binary;
using System.IO.Compression;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Compresses data into a zlib stream (RFC 1950) — the framing /FlateDecode expects — around the runtime's
/// <see cref="DeflateStream"/>.
/// </summary>
internal sealed class ZlibWriter : IDisposable
{
    private readonly MemoryStream _output = new MemoryStream();
    private readonly DeflateStream _deflater;
    private uint _checksum = Adler32.Initial;

    public ZlibWriter()
    {
        _output.WriteByte(ZlibEncoder.Method);
        _output.WriteByte(ZlibEncoder.DefaultFlags);
        _deflater = new DeflateStream(_output, CompressionLevel.Optimal, leaveOpen: true);
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        _checksum = Adler32.Append(_checksum, buffer.AsSpan(offset, count));
        _deflater.Write(buffer, offset, count);
    }

    /// <summary>Completes the stream and returns it. The writer cannot be used afterwards.</summary>
    public byte[] Finish()
    {
        _deflater.Dispose();

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, _checksum);
        foreach (byte value in checksum)
            _output.WriteByte(value);

        return _output.ToArray();
    }

    public void Dispose()
    {
        _deflater.Dispose();
        _output.Dispose();
    }
}
