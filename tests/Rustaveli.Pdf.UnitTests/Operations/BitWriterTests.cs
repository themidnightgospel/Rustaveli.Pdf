using Rustaveli.Pdf.Operations.Linearization;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Packing values of any width into bytes, most significant bit first, as hint tables pack them.
/// </summary>
public class BitWriterTests
{
    [Fact]
    public void PacksValuesAcrossByteBoundaries()
    {
        BitWriter writer = new BitWriter();
        writer.Write(0b101, 3);
        writer.Write(0b11110000, 8);
        writer.Write(1, 1);

        Assert.Equal(2, writer.Length);
        Assert.Equal<byte>([0b1011_1110, 0b0001_0000], writer.ToArray());
    }

    [Fact]
    public void WritesThirtyTwoBitValuesWhole()
    {
        BitWriter writer = new BitWriter();
        writer.Write(0x12345678, 32);
        writer.Write(0xFFFFFFFF, 32);

        Assert.Equal<byte>([0x12, 0x34, 0x56, 0x78, 0xFF, 0xFF, 0xFF, 0xFF], writer.ToArray());
    }

    [Fact]
    public void AligningStartsTheNextValueOnAByte()
    {
        BitWriter writer = new BitWriter();
        writer.Write(1, 1);
        writer.Align();
        writer.Align();
        writer.Write(1, 1);
        writer.Write(5, 0);

        Assert.Equal<byte>([0b1000_0000, 0b1000_0000], writer.ToArray());
    }

    [Fact]
    public void NothingWrittenIsNoBytes()
    {
        BitWriter writer = new BitWriter();
        writer.Align();

        Assert.Equal(0, writer.Length);
        Assert.Empty(writer.ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(255, 8)]
    [InlineData(256, 9)]
    [InlineData(-1, 0)]
    public void WidthIsTheBitsEveryValueUpToItNeeds(long value, int bits) => Assert.Equal(bits, BitWriter.Width(value));
}
