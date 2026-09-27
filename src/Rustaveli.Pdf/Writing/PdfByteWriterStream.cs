namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A write-only <see cref="Stream"/> that appends to a <see cref="PdfByteWriter"/>, so <c>DeflateStream</c> can
/// compress straight into pooled memory instead of through a <c>MemoryStream</c> and a copy.
/// </summary>
internal sealed class PdfByteWriterStream(PdfByteWriter target) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => target.Write(buffer.AsSpan(offset, count));

#if NET
    public override void Write(ReadOnlySpan<byte> buffer) => target.Write(buffer);
#endif
}
