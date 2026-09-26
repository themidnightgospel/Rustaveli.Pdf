using System.Buffers;
using System.IO.Compression;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class ZlibReaderTests
{
    private static readonly byte[] Text = Enumerable.Range(0, 5000).Select(index => (byte)((index * 31) % 251)).ToArray();

    private static string Fails(byte[] zlib) =>
        Assert.Throws<ImageFormatException>(() => ZlibReader.InflateAll(zlib, int.MaxValue)).Message;

    [Theory]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Fastest)]
    public void InflatesWhatDeflateWrote(CompressionLevel level)
    {
        Assert.Equal(Text, ZlibReader.InflateAll(TestZlib.Compress(Text, level), int.MaxValue));
    }

    [Fact]
    public void InflatesAnEmptyStream()
    {
        Assert.Empty(ZlibReader.InflateAll(TestZlib.Compress([]), 0));
    }

    [Fact]
    public void RefusesToExpandBeyondTheLimit()
    {
        byte[] zlib = TestZlib.Compress(Text);

        Assert.Contains("expands beyond 4999 bytes", Assert.Throws<ImageFormatException>(() => ZlibReader.InflateAll(zlib, 4999)).Message);
        Assert.Equal(5000, ZlibReader.InflateAll(zlib, 5000).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void RejectsAStreamTooShortForItsFraming(int length)
    {
        Assert.Contains("too short to be a zlib stream", Fails(TestZlib.Compress([]).AsSpan(0, length).ToArray()));
    }

    [Theory]
    [InlineData(0x77, 0x9C)] // compression method 7
    [InlineData(0x79, 0x9C)] // compression method 9
    [InlineData(0x88, 0x98)] // a 64 KiB window, which deflate does not have
    [InlineData(0x78, 0x9D)] // check bits that do not make a multiple of 31
    [InlineData(0x78, 0xBB)] // a preset dictionary, with valid check bits
    public void RejectsAnInvalidHeader(byte method, byte flags)
    {
        byte[] zlib = TestZlib.Compress(Text);
        zlib[0] = method;
        zlib[1] = flags;

        Assert.Contains("valid zlib header", Fails(zlib));
    }

    [Theory]
    [InlineData(0x08, 0x1D)] // the smallest window
    [InlineData(0x78, 0x01)] // fastest-compression level bits
    [InlineData(0x78, 0xDA)] // best-compression level bits
    public void AcceptsEveryValidHeader(byte method, byte flags)
    {
        byte[] zlib = TestZlib.Compress(Text);
        zlib[0] = method;
        zlib[1] = flags;

        Assert.Equal(Text, ZlibReader.InflateAll(zlib, int.MaxValue));
    }

    [Fact]
    public void RejectsAChecksumMismatch()
    {
        byte[] zlib = TestZlib.Compress(Text);
        zlib[zlib.Length - 2] ^= 0x01;

        Assert.Contains("fails its Adler-32 checksum", Fails(zlib));
    }

    [Fact]
    public void ReportsCorruptDeflateDataAsAFormatError()
    {
        // Block type 3 is reserved: no deflate decoder accepts it.
        byte[] zlib = [0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x01];

        ImageFormatException error = Assert.Throws<ImageFormatException>(() => ZlibReader.InflateAll(zlib, int.MaxValue));

        Assert.Contains("compressed data is corrupt", error.Message);
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void ReadsExactlyWhatIsAskedFor()
    {
        using ZlibReader reader = new ZlibReader(TestZlib.Compress(Text));
        byte[] buffer = new byte[6000];

        reader.ReadExactly(buffer, 10, 4000);
        reader.ReadExactly(buffer, 4010, 1000);
        reader.Finish();

        Assert.Equal(Text, buffer.AsSpan(10, 5000).ToArray());
    }

    [Fact]
    public void RejectsAReadPastTheEnd()
    {
        using ZlibReader reader = new ZlibReader(TestZlib.Compress(Text));
        byte[] buffer = new byte[5001];

        ImageFormatException error = Assert.Throws<ImageFormatException>(() => reader.ReadExactly(buffer, 0, 5001));

        Assert.Contains("ends before all of the image has been read", error.Message);
    }

    [Fact]
    public void ChecksTheChecksumOverDataTheCallerDidNotRead()
    {
        byte[] zlib = TestZlib.Compress(Text);
        using ZlibReader reader = new ZlibReader(zlib);
        reader.ReadExactly(new byte[100], 0, 100);

        reader.Finish();

        zlib[zlib.Length - 1] ^= 0x80;
        using ZlibReader damaged = new ZlibReader(zlib);
        damaged.ReadExactly(new byte[100], 0, 100);
        Assert.Throws<ImageFormatException>(damaged.Finish);
    }

    [Fact]
    public void ReturnsZeroAtTheEnd()
    {
        using ZlibReader reader = new ZlibReader(TestZlib.Compress([1, 2, 3]));
        byte[] buffer = new byte[10];

        Assert.Equal(3, reader.Read(buffer, 0, 10));
        Assert.Equal(0, reader.Read(buffer, 0, 10));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.AsSpan(0, 3).ToArray());
    }

    [Fact]
    public void ReadsMemoryThatIsNotBackedByAnArray()
    {
        using UnmanagedLikeMemory memory = new UnmanagedLikeMemory(TestZlib.Compress(Text));

        Assert.Equal(Text, ZlibReader.InflateAll(memory.Memory, int.MaxValue));
    }

    [Fact]
    public void ReadsASliceOfALargerArray()
    {
        byte[] zlib = TestZlib.Compress(Text);
        byte[] padded = [9, 9, 9, .. zlib, 9, 9];

        Assert.Equal(Text, ZlibReader.InflateAll(new ReadOnlyMemory<byte>(padded, 3, zlib.Length), int.MaxValue));
    }

    [Fact]
    public void WritesAStreamTheReaderAndDeflateBothAccept()
    {
        using ZlibWriter writer = new ZlibWriter();
        writer.Write(Text, 0, 2000);
        writer.Write(Text, 2000, 3000);
        byte[] zlib = writer.Finish();

        Assert.Equal(0x78, zlib[0]);
        Assert.Equal(0x9C, zlib[1]);
        Assert.Equal(Text, TestZlib.Decompress(zlib));
        Assert.Equal(Text, ZlibReader.InflateAll(zlib, int.MaxValue));
    }

    [Fact]
    public void WritesAnEmptyStream()
    {
        using ZlibWriter writer = new ZlibWriter();

        byte[] zlib = writer.Finish();

        Assert.Empty(TestZlib.Decompress(zlib));
        Assert.Equal(new byte[] { 0, 0, 0, 1 }, zlib.AsSpan(zlib.Length - 4).ToArray());
    }

    /// <summary>Memory whose owner does not expose an array, as native or pooled-segment memory would not.</summary>
    private sealed class UnmanagedLikeMemory(byte[] data) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => data;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
