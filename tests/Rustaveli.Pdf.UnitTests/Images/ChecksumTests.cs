using System.Text;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// CRC-32, Adler-32 and XXH64 against published or independently computed values (zlib's crc32 and adler32, the
/// reference xxhash library), over lengths that reach every branch of each algorithm.
/// </summary>
public class ChecksumTests
{
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Counting(int length) => Enumerable.Range(0, length).Select(index => (byte)index).ToArray();

    private static byte[] Sevens(int length) => Enumerable.Range(0, length).Select(index => (byte)(index * 7)).ToArray();

    [Theory]
    [InlineData("", 0x00000000u)]
    [InlineData("a", 0xE8B7BE43u)]
    [InlineData("123456789", 0xCBF43926u)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Crc32MatchesKnownValues(string text, uint expected)
    {
        Assert.Equal(expected, Crc32.Compute(Ascii(text)));
    }

    [Fact]
    public void Crc32MatchesZlibOverLongInput()
    {
        Assert.Equal(0x74E3FB41u, Crc32.Compute(Counting(1000)));
        Assert.Equal(0x0EAF0153u, Crc32.Compute(Sevens(100000)));
    }

    [Fact]
    public void Crc32AppendsAcrossAnySplit()
    {
        byte[] data = Sevens(100);
        uint whole = Crc32.Compute(data);

        for (int split = 0; split <= data.Length; split++)
            Assert.Equal(whole, Crc32.Append(Crc32.Compute(data.AsSpan(0, split)), data.AsSpan(split)));
    }

    [Fact]
    public void Crc32AgreesWithTheBitwiseDefinitionAtEveryLength()
    {
        byte[] data = Sevens(40);
        for (int length = 0; length <= data.Length; length++)
            Assert.Equal(TestPng.Crc(data.AsSpan(0, length)), Crc32.Compute(data.AsSpan(0, length)));
    }

    [Theory]
    [InlineData("", 0x00000001u)]
    [InlineData("a", 0x00620062u)]
    [InlineData("123456789", 0x091E01DEu)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x5BDC0FDAu)]
    public void Adler32MatchesKnownValues(string text, uint expected)
    {
        Assert.Equal(expected, Adler32.Append(Adler32.Initial, Ascii(text)));
    }

    [Fact]
    public void Adler32ReducesBothSumsAcrossBlocks()
    {
        Assert.Equal(0x1D03E73Cu, Adler32.Append(Adler32.Initial, Counting(1000)));
        Assert.Equal(0x6A10942Fu, Adler32.Append(Adler32.Initial, Sevens(100000)));

        // All 0xFF drives both sums to their maximum, where a missed or late reduction would overflow.
        Assert.Equal(0x9F51D664u, Adler32.Append(Adler32.Initial, Enumerable.Repeat((byte)0xFF, 20000).ToArray()));
    }

    [Fact]
    public void Adler32AppendsAcrossAnySplit()
    {
        byte[] data = Sevens(12000);
        uint whole = Adler32.Append(Adler32.Initial, data);

        foreach (int split in new[] { 0, 1, 5551, 5552, 5553, 11104, 12000 })
            Assert.Equal(whole, Adler32.Append(Adler32.Append(Adler32.Initial, data.AsSpan(0, split)), data.AsSpan(split)));
    }

    [Theory]
    [InlineData("", 0xEF46DB3751D8E999UL)]
    [InlineData("a", 0xD24EC4F1A98C6E5BUL)]
    [InlineData("abc", 0x44BC2CF5AD770999UL)]
    [InlineData("abcd", 0xDE0327B0D25D92CCUL)]
    [InlineData("abcdefgh", 0x3AD351775B4634B7UL)]
    [InlineData("Nobody inspects the spammish repetition", 0xFBCEA83C8A378BF1UL)]
    public void XxHash64MatchesTheReferenceForShortInput(string text, ulong expected)
    {
        Assert.Equal(expected, XxHash64.Hash(Ascii(text)));
    }

    [Theory]
    [InlineData(32, 0xCBF59C5116FF32B4UL)]
    [InlineData(33, 0x0C535D1ACAFB8EADUL)]
    [InlineData(64, 0xF7C67301DB6713F0UL)]
    [InlineData(100, 0x6AC1E58032166597UL)]
    [InlineData(1000, 0x6EF436B00EBA4078UL)]
    public void XxHash64MatchesTheReferenceForStripedInput(int length, ulong expected)
    {
        Assert.Equal(expected, XxHash64.Hash(Counting(length)));
    }

    [Fact]
    public void ContentHashesAreEqualOnlyForTheSameValueAndLength()
    {
        ImageContentHash hash = new ImageContentHash(0x0123456789ABCDEFUL, 42);

        Assert.True(hash == new ImageContentHash(0x0123456789ABCDEFUL, 42));
        Assert.False(hash != new ImageContentHash(0x0123456789ABCDEFUL, 42));
        Assert.True(hash != new ImageContentHash(0x0123456789ABCDEFUL, 43));
        Assert.True(hash != new ImageContentHash(0x0123456789ABCDEEUL, 42));
        Assert.True(hash.Equals((object)new ImageContentHash(0x0123456789ABCDEFUL, 42)));
        Assert.False(hash.Equals((object)"0123456789abcdef-42"));
        Assert.False(hash.Equals(null));
    }

    [Fact]
    public void ContentHashesDescribeThemselves()
    {
        ImageContentHash hash = new ImageContentHash(0x0123456789ABCDEFUL, 42);

        Assert.Equal(0x0123456789ABCDEFUL, hash.Value);
        Assert.Equal(42, hash.Length);
        Assert.Equal(unchecked((int)0x89ABCDEF), hash.GetHashCode());
        Assert.Equal("0123456789abcdef-42", hash.ToString());
        Assert.Equal("000000000000000f-0", new ImageContentHash(15, 0).ToString());
    }

    [Fact]
    public void ContentHashOfDataHashesItAndRecordsItsLength()
    {
        ImageContentHash hash = ImageContentHash.Of(Ascii("abc"));

        Assert.Equal(0x44BC2CF5AD770999UL, hash.Value);
        Assert.Equal(3, hash.Length);
    }
}
