using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// Loading images from each source the loader accepts. The fixtures used here are 48 × 32, so a width and height
/// swapped anywhere along the way cannot pass.
/// </summary>
public class RasterImageTests
{
    private const string Jpeg = "jpeg-baseline.jpg";
    private const string Png = "png-iccp.png";

    [Theory]
    [InlineData(Jpeg, (int)ImageFormat.Jpeg)]
    [InlineData(Png, (int)ImageFormat.Png)]
    public void FromBytesReadsTheSizeWithoutDecoding(string name, int format)
    {
        RasterImage image = RasterImage.FromBytes(TestImageFiles.Bytes(name));

        Assert.Equal((ImageFormat)format, image.Format);
        Assert.Equal(48, image.PixelWidth);
        Assert.Equal(32, image.PixelHeight);
    }

    [Fact]
    public void IsAnImageTheLayoutCanMeasure()
    {
        IImage image = RasterImage.FromBytes(TestImageFiles.Bytes(Png));

        Assert.Equal(48, image.PixelWidth);
        Assert.Equal(32, image.PixelHeight);
    }

    [Fact]
    public void FromBytesCopiesTheCallersArray()
    {
        byte[] data = TestImageFiles.Bytes(Jpeg);
        byte[] original = (byte[])data.Clone();

        RasterImage image = RasterImage.FromBytes(data);
        Array.Clear(data, 0, data.Length);

        Assert.Equal(original, image.Source.ToArray());
        Assert.Equal(original, image.Encode().Data.ToArray());
        Assert.Equal(ImageContentHash.Of(original), image.ContentHash);
    }

    [Fact]
    public void FromBytesRejectsNull()
    {
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => RasterImage.FromBytes(null!)).ParamName);
    }

    [Fact]
    public void FromStreamReadsTheRestOfASeekableStream()
    {
        byte[] data = TestImageFiles.Bytes(Png);
        using MemoryStream stream = new MemoryStream([.. "prefix"u8, .. data]);
        stream.Position = 6;

        RasterImage image = RasterImage.FromStream(stream);

        Assert.Equal(data, image.Source.ToArray());
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void FromStreamReadsAStreamThatCannotSeek()
    {
        byte[] data = TestImageFiles.Bytes(Jpeg);
        using SequentialStream stream = new SequentialStream(data, maxRead: 100);

        RasterImage image = RasterImage.FromStream(stream);

        Assert.Equal(data, image.Source.ToArray());
        Assert.Equal(48, image.PixelWidth);
        Assert.False(stream.Disposed);
    }

    [Fact]
    public void FromStreamRejectsNull()
    {
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => RasterImage.FromStream(null!)).ParamName);
    }

    [Fact]
    public void FromFileReadsTheFile()
    {
        RasterImage image = RasterImage.FromFile(TestImageFiles.PathOf(Png));

        Assert.Equal(TestImageFiles.Bytes(Png), image.Source.ToArray());
        Assert.Equal(ImageFormat.Png, image.Format);
    }

    [Fact]
    public void FromFileRejectsNull()
    {
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => RasterImage.FromFile(null!)).ParamName);
    }

    [Fact]
    public void FromFileReportsAMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rustaveli-missing-{Guid.NewGuid():N}.png");

        Assert.Throws<FileNotFoundException>(() => RasterImage.FromFile(path));
    }

    [Fact]
    public void RejectsDataInNoRecognisedFormat()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => RasterImage.FromBytes("certainly not an image"u8.ToArray()));

        Assert.IsNotType<UnsupportedImageFormatException>(error);
        Assert.Contains("not in a recognised image format", error.Message);
    }

    [Fact]
    public void RejectsEmptyData()
    {
        Assert.Throws<ImageFormatException>(() => RasterImage.FromBytes([]));
        Assert.Throws<ImageFormatException>(() => RasterImage.FromStream(new MemoryStream()));
    }

    [Theory]
    [InlineData("GIF89a\u0001\u0000\u0001\u0000", (int)ImageFormat.Gif)]
    [InlineData("RIFF\u0000\u0000\u0000\u0000WEBPVP8 ", (int)ImageFormat.WebP)]
    [InlineData("II*\u0000\u0008\u0000\u0000\u0000", (int)ImageFormat.Tiff)]
    [InlineData("\u0000\u0000\u0000\u0018ftypheic\u0000\u0000\u0000\u0000mif1heic", (int)ImageFormat.Heic)]
    [InlineData("\u0000\u0000\u0000\u0018ftypavif\u0000\u0000\u0000\u0000mif1avif", (int)ImageFormat.Avif)]
    [InlineData("BM\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000(\u0000\u0000\u0000", (int)ImageFormat.Bmp)]
    public void ReportsARecognisedFormatItCannotEmbed(string header, int format)
    {
        byte[] data = header.Select(character => (byte)character).ToArray();

        UnsupportedImageFormatException error = Assert.Throws<UnsupportedImageFormatException>(() => RasterImage.FromBytes(data));

        Assert.Equal((ImageFormat)format, error.Format);
        Assert.Contains($"{(ImageFormat)format} images cannot be embedded", error.Message);
    }

    [Fact]
    public void ReportsTheOrientationForTheLayout()
    {
        Assert.Equal(ExifOrientation.Rotate90, TestImageFiles.Load("jpeg-exif-orientation6.jpg").Orientation);
        Assert.Equal(ExifOrientation.Rotate90, TestImageFiles.Load("jpeg-exif-orientation6.jpg").Metadata.Orientation);
        Assert.Equal(ExifOrientation.Normal, TestImageFiles.Load(Png).Orientation);
    }

    [Fact]
    public void ReportsTheMetadataOfAPng()
    {
        RasterImage image = TestImageFiles.Load("ccwn2c08.png");

        Assert.Equal(1.0, image.Metadata.Gamma);
        Assert.Equal(0.64, image.Metadata.Chromaticities!.RedX);
        Assert.Null(image.Metadata.IccProfile);
    }

    [Fact]
    public void ReportsTheIccProfileOfAJpeg()
    {
        RasterImage image = TestImageFiles.Load("jpeg-icc.jpg");

        Assert.Equal(588, image.Metadata.IccProfile!.Data.Length);
        Assert.Null(image.Metadata.Gamma);
        Assert.Same(image.Metadata.IccProfile, image.Encode().ColorSpace.Profile);
    }

    [Fact]
    public void EmbedsAJpegAsTheBytesItWasLoadedFrom()
    {
        byte[] data = TestImageFiles.Bytes("jpeg-progressive.jpg");

        EncodedImage encoded = RasterImage.FromStream(new SequentialStream(data)).Encode();

        Assert.Equal(ImageFilter.Dct, encoded.Filter);
        Assert.Equal(data, encoded.Data.ToArray());
    }

    [Fact]
    public void EncodesOnceAndKeepsTheResult()
    {
        RasterImage image = TestImageFiles.Load("basn6a08.png");

        EncodedImage[] results = new EncodedImage[16];
        Parallel.For(0, results.Length, index => results[index] = image.Encode());

        Assert.All(results, result => Assert.Same(results[0], result));
        Assert.NotNull(results[0].SoftMask);
    }

    [Fact]
    public void DefersDecodingUntilTheEncodingIsNeeded()
    {
        // Interlaced, with image data that is not a deflate stream: loading succeeds because nothing is inflated yet;
        // the failure surfaces, as the defined exception, only when the encoding is asked for — every time.
        byte[] garbage = Enumerable.Range(0, 64).Select(index => (byte)(index * 11)).ToArray();
        byte[] file = TestPng.Build(TestPng.Header(48, 32, 8, 2, interlace: 1), TestPng.Data(garbage), TestPng.End());

        RasterImage image = RasterImage.FromBytes(file);

        Assert.Equal(48, image.PixelWidth);
        Assert.Throws<ImageFormatException>(image.Encode);
        Assert.Throws<ImageFormatException>(image.Encode);
    }

    [Fact]
    public void GivesIdenticalBytesTheSameHash()
    {
        RasterImage first = RasterImage.FromBytes(TestImageFiles.Bytes(Png));
        RasterImage second = RasterImage.FromStream(new SequentialStream(TestImageFiles.Bytes(Png)));
        RasterImage other = TestImageFiles.Load("basn2c08.png");

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, other.ContentHash);
        Assert.Equal(TestImageFiles.Bytes(Png).Length, first.ContentHash.Length);
    }

    [Fact]
    public void ConfirmsSameContentByteForByte()
    {
        byte[] data = TestImageFiles.Bytes(Png);
        RasterImage first = RasterImage.FromBytes(data);
        RasterImage second = RasterImage.FromBytes(data);

        // One byte of the image data differs; the CRC is repaired so that the altered file still loads.
        byte[] altered = (byte[])data.Clone();
        altered[data.Length - 13 - 20] ^= 1;
        RasterImage third = RasterImage.FromBytes(TestPng.RepairCrcs(altered));

        Assert.True(first.HasSameContent(second));
        Assert.True(first.HasSameContent(first));
        Assert.False(first.HasSameContent(third));
        Assert.False(first.HasSameContent(TestImageFiles.Load(Jpeg)));
    }

    [Fact]
    public void HasSameContentRejectsNull()
    {
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => TestImageFiles.Load(Png).HasSameContent(null!)).ParamName);
    }

    [Fact]
    public void ReadsAStreamThatDeliversLessThanItsLength()
    {
        byte[] data = TestImageFiles.Bytes(Jpeg);

        byte[] read = ImageSourceReader.ReadAll(new OverstatedStream(data, extraLength: 100));

        Assert.Equal(data, read);
    }

    [Fact]
    public void ReadsNothingFromAStreamPositionedAtOrPastItsEnd()
    {
        using MemoryStream stream = new MemoryStream(new byte[10]);
        stream.Position = 10;
        Assert.Empty(ImageSourceReader.ReadAll(stream));

        stream.Position = 20;
        Assert.Empty(ImageSourceReader.ReadAll(stream));
    }

    [Fact]
    public void RefusesASeekableStreamLongerThanTheLimit()
    {
        using MemoryStream stream = new MemoryStream(new byte[101]);

        Assert.Contains("larger than the 100 bytes supported", Assert.Throws<ImageFormatException>(() => ImageSourceReader.ReadAll(stream, 100)).Message);

        stream.Position = 1;
        Assert.Equal(100, ImageSourceReader.ReadAll(stream, 100).Length);
    }

    [Fact]
    public void RefusesASequentialStreamLongerThanTheLimit()
    {
        Assert.Throws<ImageFormatException>(() => ImageSourceReader.ReadAll(new SequentialStream(new byte[101]), 100));
        Assert.Throws<ImageFormatException>(() => ImageSourceReader.ReadAll(new SequentialStream(new byte[200000]), 100000));
        Assert.Equal(100, ImageSourceReader.ReadAll(new SequentialStream(new byte[100], maxRead: 30), 100).Length);
    }

    [Fact]
    public void AllowsAGigabyteByDefault()
    {
        Assert.Equal(1L << 30, ImageLimits.MaxSourceBytes);
        Assert.Equal(1L << 28, ImageLimits.MaxPixels);
    }
}
