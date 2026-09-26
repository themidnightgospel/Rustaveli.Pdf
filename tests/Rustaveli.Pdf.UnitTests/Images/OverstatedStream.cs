namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>A seekable stream that reports more bytes than it can deliver, and delivers them a few at a time.</summary>
internal sealed class OverstatedStream(byte[] data, long extraLength) : Stream
{
    private readonly MemoryStream _inner = new MemoryStream(data);

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _inner.Length + extraLength;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, 7));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
