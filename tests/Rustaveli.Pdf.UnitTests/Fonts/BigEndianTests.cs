using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>The bounds checks every other reader relies on to turn a short buffer into a FontFormatException.</summary>
public class BigEndianTests
{
    private static readonly byte[] Data = [0x12, 0x34, 0x56, 0x78, 0x9A];

    [Fact]
    public void ReadsBigEndianIntegers()
    {
        Assert.Equal(0x12, BigEndian.UInt8(Data, 0));
        Assert.Equal(0x1234, BigEndian.UInt16(Data, 0));
        Assert.Equal(0x345678u, BigEndian.UInt24(Data, 1));
        Assert.Equal(0x3456789Au, BigEndian.UInt32(Data, 1));
        Assert.Equal(-2, BigEndian.Int16([0xFF, 0xFE], 0));
        Assert.Equal(-1.5f, BigEndian.Fixed([0xFF, 0xFE, 0x80, 0x00], 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(5)]
    public void ReportsReadsPastEitherEndAsTruncation(int offset)
    {
        Assert.Throws<FontFormatException>(() => BigEndian.UInt16(Data, offset));
        Assert.Throws<FontFormatException>(() => BigEndian.UInt24(Data, offset));
        Assert.Throws<FontFormatException>(() => BigEndian.UInt32(Data, offset));

        if (offset != 4)
            Assert.Throws<FontFormatException>(() => BigEndian.UInt8(Data, offset));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(6, 0)]
    [InlineData(2, 4)]
    public void RejectsSlicesOutsideTheData(long offset, long length)
    {
        Assert.Throws<FontFormatException>(() => BigEndian.Slice((ReadOnlySpan<byte>)Data, offset, length).Length);
        Assert.Throws<FontFormatException>(() => BigEndian.Slice((ReadOnlyMemory<byte>)Data, offset, length));
    }

    [Fact]
    public void SlicesWithinTheData()
    {
        Assert.Equal(new byte[] { 0x56, 0x78 }, BigEndian.Slice((ReadOnlyMemory<byte>)Data, 2, 2).ToArray());
        Assert.Equal(0, BigEndian.Slice((ReadOnlySpan<byte>)Data, 5, 0).Length);
    }

    [Fact]
    public void ChecksumsPadTheLastWordWithZeros()
    {
        Assert.Equal(0x12345678u + 0x9A000000u, SfntWriter.Checksum(Data));
        Assert.Equal(0u, SfntWriter.Checksum([]));
    }
}
