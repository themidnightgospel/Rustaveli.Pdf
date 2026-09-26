using System.Text;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class ImageFormatDetectorTests
{
    private static ImageFormat Detect(byte[] data) => ImageFormatDetector.Detect(data);

    private static byte[] Hex(string hex) =>
        Enumerable.Range(0, hex.Length / 2).Select(index => Convert.ToByte(hex.Substring(index * 2, 2), 16)).ToArray();

    // An ISO base media 'ftyp' box: size, type, major brand, minor version, compatible brands.
    private static byte[] FileType(string major, uint? size = null, params string[] compatible)
    {
        byte[] data = new byte[16 + (compatible.Length * 4)];
        TestPng.WriteUInt32(data, 0, size ?? (uint)data.Length);
        Encoding.ASCII.GetBytes("ftyp" + major, 0, 8, data, 4);
        for (int index = 0; index < compatible.Length; index++)
            Encoding.ASCII.GetBytes(compatible[index], 0, 4, data, 16 + (index * 4));
        return data;
    }

    // A 14-byte file header, then the size of the info header that follows it (little-endian).
    private static byte[] Bitmap(uint infoHeaderSize)
    {
        byte[] data = new byte[18];
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BitConverter.GetBytes(infoHeaderSize).CopyTo(data, 14);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(data, 14, 4);
        return data;
    }

    [Theory]
    [InlineData("ffd8ff", (int)ImageFormat.Jpeg)]
    [InlineData("ffd8ffe000104a464946", (int)ImageFormat.Jpeg)]
    [InlineData("89504e470d0a1a0a", (int)ImageFormat.Png)]
    [InlineData("474946383761", (int)ImageFormat.Gif)]
    [InlineData("474946383961", (int)ImageFormat.Gif)]
    [InlineData("524946460000000057454250", (int)ImageFormat.WebP)]
    [InlineData("49492a00", (int)ImageFormat.Tiff)]
    [InlineData("4d4d002a", (int)ImageFormat.Tiff)]
    [InlineData("49492b00", (int)ImageFormat.Tiff)]
    [InlineData("4d4d002b", (int)ImageFormat.Tiff)]
    [InlineData("", (int)ImageFormat.Unknown)]
    [InlineData("ffd8", (int)ImageFormat.Unknown)]
    [InlineData("ffd9ff", (int)ImageFormat.Unknown)]
    [InlineData("ff d8fe", (int)ImageFormat.Unknown)]
    [InlineData("89504e470d0a1a", (int)ImageFormat.Unknown)]
    [InlineData("89504e470d0a1a0b", (int)ImageFormat.Unknown)]
    [InlineData("474946383861", (int)ImageFormat.Unknown)]
    [InlineData("5249464600000000415649", (int)ImageFormat.Unknown)]
    [InlineData("524946460000000041564920", (int)ImageFormat.Unknown)]
    [InlineData("49492a", (int)ImageFormat.Unknown)]
    [InlineData("49492c00", (int)ImageFormat.Unknown)]
    public void RecognisesSignatures(string hex, int expected)
    {
        Assert.Equal((ImageFormat)expected, Detect(Hex(hex.Replace(" ", string.Empty))));
    }

    [Theory]
    [InlineData(12u)]
    [InlineData(40u)]
    [InlineData(52u)]
    [InlineData(56u)]
    [InlineData(64u)]
    [InlineData(108u)]
    [InlineData(124u)]
    public void RecognisesABitmapByItsInfoHeaderSize(uint infoHeaderSize)
    {
        Assert.Equal(ImageFormat.Bmp, Detect(Bitmap(infoHeaderSize)));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(11u)]
    [InlineData(41u)]
    [InlineData(125u)]
    [InlineData(0x28000000u)]
    public void RejectsBMFollowedByAnythingElse(uint infoHeaderSize)
    {
        Assert.Equal(ImageFormat.Unknown, Detect(Bitmap(infoHeaderSize)));
    }

    [Fact]
    public void RejectsABitmapHeaderTooShortToHoldTheInfoHeaderSize()
    {
        Assert.Equal(ImageFormat.Unknown, Detect(Bitmap(40).AsSpan(0, 17).ToArray()));
    }

    [Fact]
    public void RequiresBothLettersOfTheBitmapSignature()
    {
        byte[] data = Bitmap(40);
        data[1] = (byte)'A';
        Assert.Equal(ImageFormat.Unknown, Detect(data));
        data[1] = (byte)'M';
        data[0] = (byte)'C';
        Assert.Equal(ImageFormat.Unknown, Detect(data));
    }

    [Theory]
    [InlineData("heic", (int)ImageFormat.Heic)]
    [InlineData("heix", (int)ImageFormat.Heic)]
    [InlineData("hevc", (int)ImageFormat.Heic)]
    [InlineData("hevx", (int)ImageFormat.Heic)]
    [InlineData("heim", (int)ImageFormat.Heic)]
    [InlineData("heis", (int)ImageFormat.Heic)]
    [InlineData("mif1", (int)ImageFormat.Heic)]
    [InlineData("msf1", (int)ImageFormat.Heic)]
    [InlineData("avif", (int)ImageFormat.Avif)]
    [InlineData("avis", (int)ImageFormat.Avif)]
    [InlineData("isom", (int)ImageFormat.Unknown)]
    [InlineData("mp42", (int)ImageFormat.Unknown)]
    public void RecognisesTheMajorBrand(string brand, int expected)
    {
        Assert.Equal((ImageFormat)expected, Detect(FileType(brand)));
    }

    [Fact]
    public void RecognisesTheShortestFileTypeBox()
    {
        Assert.Equal(ImageFormat.Heic, Detect(FileType("heic").AsSpan(0, 12).ToArray()));
        Assert.Equal(ImageFormat.Unknown, Detect(FileType("heic").AsSpan(0, 11).ToArray()));
    }

    [Fact]
    public void DoesNotReadTheMinorVersionAsABrand()
    {
        byte[] data = FileType("mif1");
        Encoding.ASCII.GetBytes("avif", 0, 4, data, 12);

        Assert.Equal(ImageFormat.Heic, Detect(data));
    }

    [Fact]
    public void ReadsTheCompatibleBrandThatFollowsTheMinorVersion()
    {
        Assert.Equal(ImageFormat.Avif, Detect(FileType("isom", null, "avif")));
        Assert.Equal(ImageFormat.Heic, Detect(FileType("isom", null, "heic", "avif").AsSpan(0, 20).ToArray()));
    }

    [Fact]
    public void AnAvifCompatibleBrandOutranksAGenericHeifMajorBrand()
    {
        Assert.Equal(ImageFormat.Avif, Detect(FileType("mif1", null, "miaf", "avif")));
    }

    [Fact]
    public void AHeifCompatibleBrandIdentifiesAnOtherwiseUnknownFile()
    {
        Assert.Equal(ImageFormat.Heic, Detect(FileType("isom", null, "iso8", "heic")));
    }

    [Fact]
    public void TheFirstRecognisedBrandWins()
    {
        Assert.Equal(ImageFormat.Heic, Detect(FileType("heic", null, "mp42", "mif1")));
    }

    [Fact]
    public void BrandsBeyondTheBoxAreIgnored()
    {
        // The box claims 16 bytes, so the 'avif' that follows belongs to the next box.
        Assert.Equal(ImageFormat.Heic, Detect(FileType("mif1", 16, "avif")));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(11u)]
    public void ABoxSizeTooSmallForBrandsLeavesOnlyTheMajorBrand(uint size)
    {
        Assert.Equal(ImageFormat.Heic, Detect(FileType("mif1", size, "avif")));
        Assert.Equal(ImageFormat.Unknown, Detect(FileType("isom", size, "heic")));
    }

    [Fact]
    public void BrandListsAreReadOnlyAsFarAsTheCap()
    {
        string[] filler = Enumerable.Repeat("iso8", 59).ToArray();

        // Brands up to byte 256 of the box are read; one starting at byte 256 is not.
        Assert.Equal(ImageFormat.Avif, Detect(FileType("isom", null, [.. filler, "avif"])));
        Assert.Equal(ImageFormat.Unknown, Detect(FileType("isom", null, [.. filler, "iso9", "avif"])));
    }

    [Fact]
    public void ATruncatedBrandIsNotRead()
    {
        byte[] data = FileType("isom", null, "avif");
        Assert.Equal(ImageFormat.Unknown, Detect(data.AsSpan(0, data.Length - 1).ToArray()));
    }

    [Fact]
    public void EveryFixtureIsRecognisedByItsExtension()
    {
        foreach (string name in TestImageFiles.Valid())
        {
            ImageFormat expected = name.EndsWith(".jpg", StringComparison.Ordinal) ? ImageFormat.Jpeg : ImageFormat.Png;
            Assert.Equal(expected, Detect(TestImageFiles.Bytes(name)));
        }
    }
}
