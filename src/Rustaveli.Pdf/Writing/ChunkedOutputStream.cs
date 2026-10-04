using System.Buffers;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A write-only <see cref="Stream"/> that keeps what is written in pooled chunks, for an export that returns the file
/// as one array. <see cref="ToArray"/> copies it out once, where a <c>MemoryStream</c> doubles its buffer as it grows,
/// copying at every step and putting the larger steps on the large object heap, and then copies it again.
/// </summary>
/// <remarks>Disposing it returns the chunks to the pool, so what was written does not survive it.</remarks>
internal sealed class ChunkedOutputStream : Stream
{
    /// <summary>The size of each chunk: large enough that a file takes few, small enough to stay off the large object heap.</summary>
    internal const int ChunkSize = 64 * 1024;

    private readonly long _maximumLength;
    private readonly List<byte[]> _chunks = [];

    // Bytes used in the last chunk, and in all of them.
    private int _used;
    private long _length;
    private bool _disposed;

    public ChunkedOutputStream()
        : this(int.MaxValue)
    {
    }

    /// <summary>A stream holding at most <paramref name="maximumLength"/> bytes; a <c>MemoryStream</c> holds <see cref="int.MaxValue"/>.</summary>
    internal ChunkedOutputStream(long maximumLength) => _maximumLength = maximumLength;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed;

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

    public override void Write(byte[] buffer, int offset, int count)
    {
#if NET
        ValidateBufferArguments(buffer, offset, count);
#else
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (buffer.Length - offset < count)
            throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
#endif

        Append(buffer.AsSpan(offset, count));
    }

#if NET
    public override void Write(ReadOnlySpan<byte> buffer) => Append(buffer);
#endif

    public override void WriteByte(byte value)
    {
        Span<byte> one = stackalloc byte[1];
        one[0] = value;
        Append(one);
    }

    /// <summary>Everything written, in one array exactly as long as it.</summary>
    public byte[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_length == 0)
            return [];

#if NET
        byte[] file = GC.AllocateUninitializedArray<byte>((int)_length);
#else
        byte[] file = new byte[_length];
#endif
        int position = 0;

        for (int index = 0; index < _chunks.Count; index++)
        {
            // Only what was written counts: the pool may lend a chunk longer than was asked for.
            int count = index == _chunks.Count - 1 ? _used : ChunkSize;
            Buffer.BlockCopy(_chunks[index], 0, file, position, count);
            position += count;
        }

        return file;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            foreach (byte[] chunk in _chunks)
                ArrayPool<byte>.Shared.Return(chunk);

            _chunks.Clear();
        }

        base.Dispose(disposing);
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // A MemoryStream refuses to grow past its limit with this exception, before writing anything.
        if (_length + bytes.Length > _maximumLength)
            throw new IOException("Stream was too long.");

        while (!bytes.IsEmpty)
        {
            if (_chunks.Count == 0 || _used == ChunkSize)
            {
                byte[] chunk = ArrayPool<byte>.Shared.Rent(ChunkSize);

                try
                {
                    _chunks.Add(chunk);
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(chunk);
                    throw;
                }

                _used = 0;
            }

            int count = Math.Min(bytes.Length, ChunkSize - _used);
            bytes.Slice(0, count).CopyTo(_chunks[^1].AsSpan(_used));
            _used += count;
            _length += count;
            bytes = bytes.Slice(count);
        }
    }
}
