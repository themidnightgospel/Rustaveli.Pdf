namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// A readable stream that cannot seek or report its length, as network, decompression and HTTP response streams
/// cannot; optionally one that serves at most a few bytes per read, as they often do.
/// </summary>
internal sealed class SequentialStream(byte[] data, int maxRead = int.MaxValue) : Stream
{
    private readonly MemoryStream _inner = new MemoryStream(data);

    public bool Disposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, maxRead));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
