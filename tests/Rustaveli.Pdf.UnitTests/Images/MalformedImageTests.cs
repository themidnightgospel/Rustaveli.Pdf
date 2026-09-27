using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// Damaged input either loads and encodes, or fails with <see cref="ImageFormatException"/> — never an index,
/// overflow or argument exception escaping from inside a parser. Deterministic, so it runs on both frameworks; the
/// randomised counterpart is in PropertyBased.
/// </summary>
public class MalformedImageTests
{
    private static readonly string[] Samples =
    [
        "jpeg-baseline.jpg", "jpeg-progressive.jpg", "jpeg-gray.jpg", "jpeg-cmyk-adobe.jpg", "jpeg-rgb-adobe.jpg",
        "jpeg-exif-orientation6.jpg", "jpeg-icc.jpg",
        "basn0g01.png", "basi0g04.png", "basn2c16.png", "basi3p02.png", "basn3p08.png", "basi4a08.png", "basn4a16.png",
        "basi6a16.png", "basn6a08.png", "tbbn3p08.png", "tm3n3p02.png", "tbwn0g16.png", "oi9n0g16.png", "s05i3p02.png",
        "exif2c08.png", "png-iccp.png",
    ];

    public static TheoryData<string> SampleNames => new TheoryData<string>(Samples);

    /// <summary>Loads and encodes <paramref name="data"/>; succeeds if that works or fails the defined way.</summary>
    internal static bool LoadsOrFailsCleanly(byte[] data)
    {
        try
        {
            RasterImage.Load(data).Encode();
            return true;
        }
        catch (ImageFormatException)
        {
            return false;
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void EveryTruncationFailsCleanly(string name)
    {
        byte[] data = TestImageFiles.Bytes(name);

        int loaded = 0;
        for (int length = 0; length < data.Length; length++)
        {
            if (LoadsOrFailsCleanly(data.AsSpan(0, length).ToArray()))
                loaded++;
        }

        // A PNG is incomplete without its IEND chunk, so no truncation of one loads.
        if (name.EndsWith(".png", StringComparison.Ordinal))
            Assert.Equal(0, loaded);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void RandomDamageFailsCleanly(string name)
    {
        byte[] original = TestImageFiles.Bytes(name);
        Random random = new Random(name.Length * 7919);

        for (int iteration = 0; iteration < 150; iteration++)
        {
            byte[] data = (byte[])original.Clone();
            int changes = random.Next(1, 5);
            for (int change = 0; change < changes; change++)
                data[random.Next(data.Length)] = (byte)random.Next(256);

            LoadsOrFailsCleanly(data);

            // Rewriting the CRCs carries the damage past the chunk checks into the code that interprets chunks.
            if (name.EndsWith(".png", StringComparison.Ordinal))
                LoadsOrFailsCleanly(TestPng.RepairCrcs(data));
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void DamageToEveryHeaderByteFailsCleanly(string name)
    {
        byte[] original = TestImageFiles.Bytes(name);
        int span = Math.Min(original.Length, 200);

        foreach (byte value in new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF })
        {
            for (int position = 0; position < span; position++)
            {
                byte[] data = (byte[])original.Clone();
                data[position] = value;
                LoadsOrFailsCleanly(name.EndsWith(".png", StringComparison.Ordinal) ? TestPng.RepairCrcs(data) : data);
            }
        }
    }

    [Theory]
    [InlineData("xc1n0g08.png")]
    [InlineData("xc9n2c08.png")]
    [InlineData("xcrn0g04.png")]
    [InlineData("xcsn0g01.png")]
    [InlineData("xd0n2c08.png")]
    [InlineData("xd3n2c08.png")]
    [InlineData("xd9n2c08.png")]
    [InlineData("xdtn0g01.png")]
    [InlineData("xhdn0g08.png")]
    [InlineData("xlfn0g04.png")]
    [InlineData("xs1n0g01.png")]
    [InlineData("xs2n0g01.png")]
    [InlineData("xs4n0g01.png")]
    [InlineData("xs7n0g01.png")]
    public void RejectsEveryCorruptSuiteFile(string name)
    {
        Assert.False(LoadsOrFailsCleanly(TestImageFiles.Bytes(name)));
    }

    [Fact]
    public void RejectsDecompressionBombsBeforeAllocating()
    {
        // A 16384 × 16384 RGBA image whose image data is a few bytes of zeros: rejected on the size check alone.
        byte[] bomb = TestPng.Image(16384, 16384, 8, 6, new byte[64]);

#if NET
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.False(LoadsOrFailsCleanly(bomb));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 1 << 20, $"Allocated {allocated} bytes.");
#else
        Assert.False(LoadsOrFailsCleanly(bomb));
#endif
    }
}
