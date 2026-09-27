using System.IO.Compression;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class ZlibEncoderTests
{
    private static byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        using PdfByteWriter destination = new PdfByteWriter();
        ZlibEncoder.Compress(data, level, destination);
        return destination.WrittenSpan.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(300_000)]
    public void RoundTripsThroughTheFrameworkInflater(int length)
    {
        byte[] data = new byte[length];
        new Random(length).NextBytes(data);

        byte[] compressed = Compress(data);

        Assert.Equal(data, ZlibReader.Inflate(compressed));
    }

    [Fact]
    public void ShrinksRepetitiveData()
    {
        byte[] data = Enumerable.Repeat((byte)'a', 10_000).ToArray();

        byte[] compressed = Compress(data);

        Assert.True(compressed.Length < 100, $"10 000 identical bytes compressed to {compressed.Length}.");
        Assert.Equal(data, ZlibReader.Inflate(compressed));
    }

    [Theory]
    [InlineData(CompressionLevel.Optimal, 0x9C)]
    [InlineData(CompressionLevel.Fastest, 0x01)]
    public void WritesAHeaderThatDeclaresDeflateAndTheEffort(CompressionLevel level, int flags)
    {
        byte[] compressed = Compress(new byte[] { 1, 2, 3 }, level);

        Assert.Equal(0x78, compressed[0]);
        Assert.Equal(flags, compressed[1]);
        Assert.Equal(0, ((compressed[0] << 8) | compressed[1]) % 31);
    }

    [Fact]
    public void EndsWithTheBigEndianChecksumOfTheInput()
    {
        byte[] data = "Wikipedia"u8.ToArray();

        byte[] compressed = Compress(data);

        Assert.Equal(new byte[] { 0x11, 0xE6, 0x03, 0x98 }, compressed.AsSpan(compressed.Length - 4).ToArray());
    }

    [Fact]
    public void AppendsAfterWhatTheDestinationAlreadyHolds()
    {
        using PdfByteWriter destination = new PdfByteWriter();
        destination.Write("prefix"u8);

        ZlibEncoder.Compress("data"u8, CompressionLevel.Optimal, destination);

        Assert.Equal("prefix", Latin1.Text(destination.WrittenSpan.Slice(0, 6)));
        Assert.Equal("data"u8.ToArray(), ZlibReader.Inflate(destination.WrittenSpan.Slice(6).ToArray()));
    }
}
