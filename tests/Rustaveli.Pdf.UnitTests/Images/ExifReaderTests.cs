using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class ExifReaderTests
{
    private static ExifOrientation Read(byte[] tiff) => ExifReader.ReadOrientation(tiff);

    [Theory]
    [InlineData(1, (int)ExifOrientation.Normal)]
    [InlineData(2, (int)ExifOrientation.FlipHorizontal)]
    [InlineData(3, (int)ExifOrientation.Rotate180)]
    [InlineData(4, (int)ExifOrientation.FlipVertical)]
    [InlineData(5, (int)ExifOrientation.Transpose)]
    [InlineData(6, (int)ExifOrientation.Rotate90)]
    [InlineData(7, (int)ExifOrientation.Transverse)]
    [InlineData(8, (int)ExifOrientation.Rotate270)]
    public void ReadsEveryOrientationInBothByteOrders(int value, int expected)
    {
        Assert.Equal((ExifOrientation)expected, Read(TestJpeg.Orientation(value, bigEndian: false)));
        Assert.Equal((ExifOrientation)expected, Read(TestJpeg.Orientation(value, bigEndian: true)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(0x0600)]
    public void TreatsAnOutOfRangeValueAsNormal(int value)
    {
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Orientation(value)));
    }

    [Fact]
    public void FindsTheOrientationAmongOtherTags()
    {
        byte[] tiff = TestJpeg.Tiff(
            true,
            (0x010F, 2, 4, 0x41424300),
            (0x0110, 2, 4, 0x44454600),
            (0x0112, 3, 1, 8),
            (0x011A, 5, 1, 0x62));

        Assert.Equal(ExifOrientation.Rotate270, Read(tiff));
    }

    [Fact]
    public void ReturnsNormalWithoutAnOrientationTag()
    {
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Tiff(false, (0x010F, 2, 4, 0x41424300))));
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Tiff(false)));
    }

    [Fact]
    public void StopsAtTheFirstOrientationTag()
    {
        byte[] tiff = TestJpeg.Tiff(false, (0x0112, 3, 1, 3), (0x0112, 3, 1, 6));

        Assert.Equal(ExifOrientation.Rotate180, Read(tiff));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    public void RequiresAShortValue(ushort type)
    {
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Tiff(false, (0x0112, type, 1, 6))));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public void RequiresExactlyOneValue(uint count)
    {
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Tiff(false, (0x0112, 3, count, 6))));
    }

    [Fact]
    public void RejectsAnUnknownByteOrder()
    {
        // Each structure is valid in the byte order its first letter names; only the second letter is wrong.
        byte[] bigEndian = TestJpeg.Orientation(6, bigEndian: true);
        bigEndian[1] = (byte)'I';
        Assert.Equal(ExifOrientation.Normal, Read(bigEndian));

        byte[] littleEndian = TestJpeg.Orientation(6, bigEndian: false);
        littleEndian[1] = (byte)'M';
        Assert.Equal(ExifOrientation.Normal, Read(littleEndian));

        byte[] swapped = TestJpeg.Orientation(6, bigEndian: false);
        swapped[0] = (byte)'M';
        Assert.Equal(ExifOrientation.Normal, Read(swapped));
    }

    [Fact]
    public void ReadsNoEntriesBeyondTheDirectoryCount()
    {
        byte[] tiff = TestJpeg.Tiff(false, (0x010F, 2, 4, 0x41424300), (0x0112, 3, 1, 6));

        // The orientation entry is there, but the count says the directory ends before it.
        tiff[8] = 1;

        Assert.Equal(ExifOrientation.Normal, Read(tiff));
    }

    [Fact]
    public void RejectsAWrongMagicNumber()
    {
        byte[] tiff = TestJpeg.Orientation(6);
        tiff[2] = 43;
        Assert.Equal(ExifOrientation.Normal, Read(tiff));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void ReturnsNormalForDataShorterThanTheHeader(int length)
    {
        Assert.Equal(ExifOrientation.Normal, Read(TestJpeg.Orientation(6).AsSpan(0, length).ToArray()));
    }

    [Fact]
    public void ReturnsNormalWhenTheDirectoryLiesOutsideTheData()
    {
        byte[] tiff = TestJpeg.Orientation(6);

        // Little-endian directory offset: one byte short of room for the entry count.
        tiff[4] = (byte)(tiff.Length - 1);
        Assert.Equal(ExifOrientation.Normal, Read(tiff));

        // An offset far beyond the data, which must not wrap around to a small one.
        tiff[4] = 0x08;
        tiff[7] = 0xFF;
        Assert.Equal(ExifOrientation.Normal, Read(tiff));
    }

    [Fact]
    public void ReadsADirectoryCountAtTheVeryEnd()
    {
        // The directory offset points at the last two bytes: a count of zero entries, which is valid.
        byte[] tiff = [(byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 0, 0];

        Assert.Equal(ExifOrientation.Normal, Read(tiff));
    }

    [Fact]
    public void StopsAtAnEntryThatRunsPastTheEnd()
    {
        byte[] tiff = TestJpeg.Tiff(false, (0x010F, 2, 4, 0x41424300), (0x0112, 3, 1, 6));

        // Header, count, first entry, then the orientation entry: complete at 34 bytes, cut short at 33.
        Assert.Equal(ExifOrientation.Rotate90, Read(tiff.AsSpan(0, 34).ToArray()));
        Assert.Equal(ExifOrientation.Normal, Read(tiff.AsSpan(0, 33).ToArray()));
    }
}
