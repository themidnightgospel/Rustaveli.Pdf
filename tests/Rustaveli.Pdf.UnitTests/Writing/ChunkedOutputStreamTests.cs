using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class ChunkedOutputStreamTests
{
    private static byte[] Bytes(int count)
    {
        byte[] bytes = new byte[count];

        for (int index = 0; index < count; index++)
            bytes[index] = (byte)(index * 31 + index / 251);

        return bytes;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ChunkedOutputStream.ChunkSize - 1)]
    [InlineData(ChunkedOutputStream.ChunkSize)]
    [InlineData(ChunkedOutputStream.ChunkSize + 1)]
    [InlineData(200_003)]
    public void ReturnsEverythingWrittenInOneCall(int count)
    {
        byte[] written = Bytes(count);
        using ChunkedOutputStream stream = new ChunkedOutputStream();

        stream.Write(written, 0, written.Length);

        Assert.Equal(written, stream.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7_001)]
    [InlineData(ChunkedOutputStream.ChunkSize)]
    [InlineData(ChunkedOutputStream.ChunkSize + 3)]
    public void ReturnsEverythingWrittenInPiecesAcrossChunkBoundaries(int piece)
    {
        byte[] written = Bytes(200_003);
        using ChunkedOutputStream stream = new ChunkedOutputStream();

        for (int offset = 0; offset < written.Length; offset += piece)
            stream.Write(written, offset, Math.Min(piece, written.Length - offset));

        Assert.Equal(written, stream.ToArray());
    }

    [Fact]
    public void WritesFromTheMiddleOfABufferAndSingleBytes()
    {
        byte[] source = Bytes(1_000);
        using ChunkedOutputStream stream = new ChunkedOutputStream();

        stream.Write(source, 100, 300);
        stream.WriteByte(42);

        Assert.Equal([.. source.Skip(100).Take(300), (byte)42], stream.ToArray());
    }

    [Fact]
    public void RefusesBadArgumentsAsAMemoryStreamDoes()
    {
        using ChunkedOutputStream stream = new ChunkedOutputStream();
        using MemoryStream memory = new MemoryStream();
        byte[] buffer = new byte[10];

        foreach (Action<Stream> write in new Action<Stream>[]
                 {
                     target => target.Write(null!, 0, 0),
                     target => target.Write(buffer, -1, 1),
                     target => target.Write(buffer, 0, -1),
                     target => target.Write(buffer, 8, 3),
                 })
        {
            Exception expected = Assert.ThrowsAny<Exception>(() => write(memory));
            Exception actual = Assert.ThrowsAny<Exception>(() => write(stream));

            Assert.IsType(expected.GetType(), actual);
            Assert.Equal((expected as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
        }

        Assert.Empty(stream.ToArray());
    }

    [Fact]
    public void IsWriteOnly()
    {
        using ChunkedOutputStream stream = new ChunkedOutputStream();

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        stream.Flush();
    }

    [Fact]
    public void DisposingTwiceIsHarmlessAndWritingAfterwardsIsRefused()
    {
        ChunkedOutputStream stream = new ChunkedOutputStream();
        stream.Write(Bytes(ChunkedOutputStream.ChunkSize + 10), 0, ChunkedOutputStream.ChunkSize + 10);

        stream.Dispose();
        stream.Dispose();

        Assert.False(stream.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.ToArray());
    }

    [Fact]
    public void RefusesToGrowPastItsLimitAsAMemoryStreamDoesAndKeepsWhatItHad()
    {
        // A MemoryStream holds at most int.MaxValue bytes and throws IOException past it; the limit here is smaller
        // only so the test need not write two gigabytes.
        byte[] written = Bytes(70_000);
        using ChunkedOutputStream stream = new ChunkedOutputStream(maximumLength: 100_000);
        stream.Write(written, 0, written.Length);

        Assert.Throws<IOException>(() => stream.Write(new byte[40_000], 0, 40_000));
        Assert.Equal(written, stream.ToArray());
    }
}
