using System.Text;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class Adler32Tests
{
    [Theory]
    [InlineData("", 0x00000001u)]
    [InlineData("a", 0x00620062u)]
    [InlineData("abc", 0x024D0127u)]
    [InlineData("Wikipedia", 0x11E60398u)]
    public void MatchesPublishedValues(string text, uint expected)
    {
        Assert.Equal(expected, Adler32.Compute(Encoding.ASCII.GetBytes(text)));
    }

    [Fact]
    public void MatchesAByteByByteReferenceAcrossManyBlocks()
    {
        byte[] data = new byte[100_000];
        new Random(1950).NextBytes(data);

        Assert.Equal(ZlibReader.ReferenceAdler32(data), Adler32.Compute(data));
    }

    [Fact]
    public void ReducesBeforeTheSumsCanOverflow()
    {
        // Worst case for the deferred reduction: the low sum ends a block one short of the modulus, then every
        // byte is 0xFF. With reduction deferred even one byte longer than the block length allows, the high sum
        // passes 2^32 and the checksum comes out wrong.
        byte[] data = new byte[3 * 5553];
        data.AsSpan(0, 256).Fill(0xFF);
        data[256] = 0xEF;
        data.AsSpan(5553).Fill(0xFF);

        Assert.Equal(ZlibReader.ReferenceAdler32(data), Adler32.Compute(data));
    }

    [Fact]
    public void ReducesEvenWhenTheDataEndsMidBlock()
    {
        byte[] data = Enumerable.Repeat((byte)0xFF, 5552 + 17).ToArray();

        Assert.Equal(ZlibReader.ReferenceAdler32(data), Adler32.Compute(data));
    }
}
