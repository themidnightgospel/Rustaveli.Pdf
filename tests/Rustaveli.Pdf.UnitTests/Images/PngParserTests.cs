using System.Runtime.InteropServices;
using System.Text;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class PngParserTests
{
    // Two rows of two 8-bit gray pixels, filtered with None, compressed: data whose content these tests ignore.
    private static readonly byte[] Pixels = TestZlib.Compress([0, 1, 2, 0, 3, 4]);

    private static PngFile Parse(byte[] data) => PngParser.Parse(data);

    private static string Fails(byte[] data) => Assert.Throws<ImageFormatException>(() => Parse(data)).Message;

    private static byte[] Header(int colorType = 0, int bitDepth = 8, int width = 2, int height = 2, int interlace = 0) =>
        TestPng.Header(width, height, bitDepth, colorType, interlace);

    private static byte[] Palette(int entries) => TestPng.Chunk("PLTE", Enumerable.Range(0, entries * 3).Select(i => (byte)i).ToArray());

    /// <summary>A 2 × 2 image of the given type: IHDR, <paramref name="chunks"/>, IDAT, IEND.</summary>
    private static byte[] Image(byte[] header, params byte[][] chunks) =>
        TestPng.Build([header, .. chunks, TestPng.Data(Pixels), TestPng.End()]);

    private static byte[] Gray(params byte[][] chunks) => Image(Header(), chunks);

    private static byte[] Chunk(string type, params byte[] data) => TestPng.Chunk(type, data);

    private static byte[] Fixed(params uint[] values)
    {
        byte[] data = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++)
            TestPng.WriteUInt32(data, index * 4, values[index]);
        return data;
    }

    [Theory]
    [InlineData("basn0g01.png", 32, 32, 1, 0, false)]
    [InlineData("basn2c16.png", 32, 32, 16, 2, false)]
    [InlineData("basi3p04.png", 32, 32, 4, 3, true)]
    [InlineData("basn4a08.png", 32, 32, 8, 4, false)]
    [InlineData("basi6a16.png", 32, 32, 16, 6, true)]
    [InlineData("s03n3p01.png", 3, 3, 1, 3, false)]
    [InlineData("png-iccp.png", 48, 32, 8, 2, false)]
    public void ReadsTheHeader(string name, int width, int height, int bitDepth, int colorType, bool interlaced)
    {
        PngHeader header = TestImageFiles.Png(name).Header;

        Assert.Equal(width, header.Width);
        Assert.Equal(height, header.Height);
        Assert.Equal(bitDepth, header.BitDepth);
        Assert.Equal((PngColorType)colorType, header.ColorType);
        Assert.Equal(interlaced, header.Interlaced);
    }

    [Theory]
    [InlineData(0, 1, 1, 1, false, 1, 1)]
    [InlineData(0, 16, 1, 1, false, 16, 2)]
    [InlineData(2, 8, 3, 3, false, 24, 3)]
    [InlineData(2, 16, 3, 3, false, 48, 6)]
    [InlineData(3, 2, 1, 1, false, 2, 1)]
    [InlineData(4, 8, 2, 1, true, 16, 2)]
    [InlineData(4, 16, 2, 1, true, 32, 4)]
    [InlineData(6, 8, 4, 3, true, 32, 4)]
    [InlineData(6, 16, 4, 3, true, 64, 8)]
    public void DerivesTheSampleLayout(int colorType, int bitDepth, int channels, int colorChannels, bool alpha,
        int bitsPerPixel, int filterStep)
    {
        PngHeader header = new PngHeader(5, 1, bitDepth, (PngColorType)colorType, false);

        Assert.Equal(channels, header.Channels);
        Assert.Equal(colorChannels, header.ColorChannels);
        Assert.Equal(alpha, header.HasAlphaChannel);
        Assert.Equal(bitsPerPixel, header.BitsPerPixel);
        Assert.Equal(filterStep, header.FilterStep);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 8, 1)]
    [InlineData(1, 9, 2)]
    [InlineData(2, 3, 1)]
    [InlineData(2, 5, 2)]
    [InlineData(4, 3, 2)]
    [InlineData(8, 3, 3)]
    [InlineData(16, 3, 6)]
    public void CountsRowBytesIncludingPartialBytes(int bitDepth, int pixels, long bytes)
    {
        Assert.Equal(bytes, new PngHeader(pixels, 1, bitDepth, PngColorType.Gray, false).RowBytes(pixels));
    }

    [Fact]
    public void CountsRowBytesBeyondTheRangeOfAnInteger()
    {
        PngHeader header = new PngHeader(1 << 28, 1, 16, PngColorType.Rgba, false);

        Assert.Equal(1L << 31, header.RowBytes(1 << 28));
    }

    [Fact]
    public void LocatesEveryIdatPayload()
    {
        byte[] file = TestImageFiles.Bytes("oi4n2c16.png");

        PngFile png = PngParser.Parse(file);

        Assert.Equal(4, png.ImageData.Count);
        Assert.Equal(new[] { 99, 29, 99, 2 }, png.ImageData.Select(segment => segment.Length));
        Assert.Equal(229, png.ImageDataLength);
        foreach (PngSegment segment in png.ImageData)
            Assert.Equal("IDAT", Encoding.ASCII.GetString(file, segment.Offset - 4, 4));
    }

    [Fact]
    public void ConcatenatesIdatPayloadsInOrder()
    {
        byte[] first = [0x78, 0x9C, 1, 2];
        byte[] second = [3, 4, 5];
        byte[] third = [6];

        PngFile png = Parse(TestPng.Build(Header(), TestPng.Data(first), TestPng.Data(second), TestPng.Data(third), TestPng.End()));

        Assert.Equal(new byte[] { 0x78, 0x9C, 1, 2, 3, 4, 5, 6 }, png.ConcatenateImageData().ToArray());
        byte[] copy = new byte[10];
        png.CopyImageData(copy);
        Assert.Equal(new byte[] { 0x78, 0x9C, 1, 2, 3, 4, 5, 6, 0, 0 }, copy);
    }

    [Fact]
    public void LeavesASingleIdatPayloadWhereItIs()
    {
        byte[] file = Gray();

        PngFile png = Parse(file);
        ReadOnlyMemory<byte> data = png.ConcatenateImageData();

        Assert.True(MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment));
        Assert.Same(file, segment.Array);
        Assert.Equal(png.ImageData[0].Offset, segment.Offset);
        Assert.Equal(Pixels, data.ToArray());
    }

    [Fact]
    public void SkipsEmptyIdatChunks()
    {
        PngFile png = Parse(TestPng.Build(Header(), TestPng.Data([]), TestPng.Data(Pixels), TestPng.Data([]), TestPng.End()));

        PngSegment segment = Assert.Single(png.ImageData);
        Assert.Equal(Pixels.Length, segment.Length);
    }

    [Fact]
    public void RejectsAFileWithOnlyEmptyIdatChunks()
    {
        Assert.Contains("no image data", Fails(TestPng.Build(Header(), TestPng.Data([]), TestPng.End())));
    }

    [Fact]
    public void RejectsAFileWithoutIdat()
    {
        Assert.Contains("no image data", Fails(TestImageFiles.Bytes("xdtn0g01.png")));
    }

    [Fact]
    public void RejectsIdatChunksThatAreNotConsecutive()
    {
        byte[] file = TestPng.Build(Header(), TestPng.Data(Pixels), Chunk("tEXt", 0x41, 0, 0x42), TestPng.Data([]), TestPng.End());

        Assert.Contains("not consecutive", Fails(file));
    }

    [Theory]
    [InlineData("xs1n0g01.png")]
    [InlineData("xs2n0g01.png")]
    [InlineData("xs4n0g01.png")]
    [InlineData("xs7n0g01.png")]
    [InlineData("xcrn0g04.png")]
    [InlineData("xlfn0g04.png")]
    public void RejectsADamagedSignature(string name)
    {
        Assert.Contains("PNG signature", Fails(TestImageFiles.Bytes(name)));
    }

    [Fact]
    public void RejectsAFileEndingBeforeIend()
    {
        byte[] file = Gray();

        Assert.Contains("ends before its IEND chunk", Fails(file.AsSpan(0, file.Length - 12).ToArray()));
        Assert.Contains("ends before its IEND chunk", Fails(file.AsSpan(0, file.Length - 1).ToArray()));
        Assert.Contains("ends before its IEND chunk", Fails(TestPng.Signature));
    }

    [Fact]
    public void RejectsAChunkRunningPastTheEnd()
    {
        byte[] file = Gray();
        byte[] cut = file.AsSpan(0, file.Length - 12 - 1).ToArray();

        Assert.Contains("runs past the end of the file", Fails(cut));
    }

    [Theory]
    [InlineData(0x80000000u)]
    [InlineData(0xFFFFFFFFu)]
    public void RejectsAChunkLengthBeyondTheFile(uint length)
    {
        byte[] file = Gray();
        TestPng.WriteUInt32(file, 8 + 25, length);

        Assert.Contains("chunk at offset 33 runs past the end", Fails(file));
    }

    [Theory]
    [InlineData("ID@T")]
    [InlineData("ID[T")]
    [InlineData("ID`T")]
    [InlineData("ID{T")]
    [InlineData("1DAT")]
    public void RejectsAChunkTypeThatIsNotFourLetters(string type)
    {
        byte[] file = Gray(Chunk(type, 1, 2));

        Assert.Contains("invalid chunk type at offset 33", Fails(file));
    }

    [Theory]
    [InlineData("xhdn0g08.png", "IHDR")]
    [InlineData("xcsn0g01.png", "IDAT")]
    public void RejectsAChunkWhoseCrcDoesNotMatch(string name, string chunk)
    {
        string message = Fails(TestImageFiles.Bytes(name));

        Assert.Contains($"PNG {chunk} chunk at offset", message);
        Assert.Contains("fails its CRC check", message);
    }

    [Fact]
    public void ChecksTheCrcOverTheChunkTypeAsWellAsItsData()
    {
        byte[] file = Gray(TestPng.Chunk("tEXt", [0x41, 0, 0x42]));

        // Changing only the type's case keeps the data intact but must still break the CRC.
        file[33 + 4] = (byte)'T';

        Assert.Contains("fails its CRC check", Fails(file));
    }

    [Fact]
    public void RejectsAFileNotStartingWithIhdr()
    {
        byte[] file = TestPng.Build(Chunk("gAMA", 0, 1, 0x86, 0xA0), Header(), TestPng.Data(Pixels), TestPng.End());

        Assert.Contains("does not start with an IHDR chunk", Fails(file));
    }

    [Fact]
    public void RejectsASecondIhdr()
    {
        Assert.Contains("more than one IHDR", Fails(Gray(Header())));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(14)]
    public void RejectsAnIhdrOfTheWrongLength(int length)
    {
        byte[] data = new byte[length];
        TestPng.HeaderData(2, 2, 8, 0).AsSpan(0, Math.Min(13, length)).CopyTo(data);

        Assert.Contains($"IHDR chunk is {length} bytes long instead of 13", Fails(Image(TestPng.Chunk("IHDR", data))));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void RejectsAnImageWithoutPixels(int width, int height)
    {
        Assert.Contains($"has no pixels ({width} × {height})", Fails(Image(Header(width: width, height: height))));
    }

    [Theory]
    [InlineData(0x80000000u, 1u)]
    [InlineData(1u, 0x80000000u)]
    [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)]
    public void RejectsDimensionsBeyondTheFormatsRange(uint width, uint height)
    {
        byte[] header = TestPng.HeaderData(1, 1, 8, 0);
        TestPng.WriteUInt32(header, 0, width);
        TestPng.WriteUInt32(header, 4, height);

        Assert.Contains("out of range", Fails(Image(TestPng.Chunk("IHDR", header))));
    }

    [Fact]
    public void RejectsTheWidestImageTheFormatAllowsByItsPixelCount()
    {
        // 2^31 - 1 wide and one pixel tall is still more than the pixel limit, which is what rejects it.
        Assert.Contains("more than the 268435456 pixels", Fails(Image(Header(width: int.MaxValue, height: 1))));
    }

    [Fact]
    public void RejectsTheTallestImageTheFormatAllowsByItsPixelCount()
    {
        Assert.StartsWith("The PNG image is 1 × 2147483647 pixels", Fails(Image(Header(width: 1, height: int.MaxValue))));
    }

    [Fact]
    public void RejectsMoreThanTheMaximumPixelCount()
    {
        Assert.Contains("16385 × 16384 pixels", Fails(Image(Header(width: 16385, height: 16384))));
        Assert.Equal(16384, Parse(Image(Header(width: 16384, height: 16384))).Header.Width);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 4)]
    [InlineData(0, 8)]
    [InlineData(0, 16)]
    [InlineData(2, 8)]
    [InlineData(2, 16)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(3, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 8)]
    [InlineData(4, 16)]
    [InlineData(6, 8)]
    [InlineData(6, 16)]
    public void AcceptsEveryLegalColourTypeAndBitDepth(int colorType, int bitDepth)
    {
        byte[][] chunks = colorType == 3 ? [Palette(2)] : [];

        PngHeader header = Parse(Image(Header(colorType, bitDepth), chunks)).Header;

        Assert.Equal((PngColorType)colorType, header.ColorType);
        Assert.Equal(bitDepth, header.BitDepth);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(0, 32)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 16)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 4)]
    [InlineData(6, 2)]
    [InlineData(6, 4)]
    [InlineData(1, 8)]
    [InlineData(5, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 8)]
    public void RejectsIllegalColourTypesAndBitDepths(int colorType, int bitDepth)
    {
        Assert.Contains($"combines colour type {colorType} with bit depth {bitDepth}", Fails(Image(Header(colorType, bitDepth))));
    }

    [Theory]
    [InlineData("xc1n0g08.png")]
    [InlineData("xc9n2c08.png")]
    [InlineData("xd0n2c08.png")]
    [InlineData("xd3n2c08.png")]
    [InlineData("xd9n2c08.png")]
    public void RejectsTheSuiteFilesWithIllegalHeaders(string name)
    {
        Assert.Contains("which is not allowed", Fails(TestImageFiles.Bytes(name)));
    }

    [Theory]
    [InlineData(1, 0, 0, "compression method 1")]
    [InlineData(0, 1, 0, "filter method 1")]
    [InlineData(0, 0, 2, "interlace method 2")]
    public void RejectsUndefinedMethods(int compression, int filter, int interlace, string expected)
    {
        byte[] header = TestPng.Chunk("IHDR", TestPng.HeaderData(2, 2, 8, 0, interlace, compression, filter));

        Assert.Contains(expected, Fails(Image(header)));
    }

    [Fact]
    public void ReadsAdam7Interlacing()
    {
        Assert.True(Parse(Image(Header(interlace: 1))).Header.Interlaced);
        Assert.False(Parse(Image(Header(interlace: 0))).Header.Interlaced);
    }

    [Fact]
    public void KeepsThePaletteOfAPaletteImage()
    {
        PngFile png = TestImageFiles.Png("basn3p02.png");

        Assert.Equal(new byte[] { 0x00, 0xFF, 0x00, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF }, png.Palette.ToArray());
    }

    [Fact]
    public void IgnoresASuggestedPaletteInATrueColourImage()
    {
        PngFile png = Parse(Image(Header(colorType: 2), Palette(4)));

        Assert.True(png.Palette.IsEmpty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public void AcceptsPalettesOfOneTo256Entries(int entries)
    {
        Assert.Equal(entries * 3, Parse(Image(Header(colorType: 3), Palette(entries))).Palette.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(257 * 3)]
    public void RejectsAPaletteOfTheWrongSize(int bytes)
    {
        byte[] palette = TestPng.Chunk("PLTE", new byte[bytes]);

        Assert.Contains($"PLTE chunk is {bytes} bytes long", Fails(Image(Header(colorType: 3), palette)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void RejectsAPaletteInAGrayImage(int colorType)
    {
        Assert.Contains("grayscale image with a PLTE chunk", Fails(Image(Header(colorType), Palette(2))));
    }

    [Fact]
    public void RejectsASecondPalette()
    {
        Assert.Contains("more than one PLTE", Fails(Image(Header(colorType: 3), Palette(2), Palette(2))));
    }

    [Fact]
    public void RejectsAPaletteAfterTheImageData()
    {
        byte[] file = TestPng.Build(Header(colorType: 3), TestPng.Data(Pixels), Palette(2), TestPng.End());

        Assert.Contains("PLTE chunk follows its image data", Fails(file));
    }

    [Fact]
    public void RejectsAPaletteImageWithoutAPalette()
    {
        Assert.Contains("palette image without a PLTE chunk", Fails(Image(Header(colorType: 3))));
    }

    [Fact]
    public void StopsAtIend()
    {
        byte[] file = [.. Gray(), .. "trailing bytes that are not a chunk"u8];

        Assert.Equal(2, Parse(file).Header.Width);
    }

    [Fact]
    public void RejectsAnUnknownCriticalChunk()
    {
        Assert.Contains("unknown critical chunk, CUST", Fails(Gray(Chunk("CUST", 1, 2, 3))));
    }

    [Theory]
    [InlineData("aAZz")]
    [InlineData("zZaA")]
    public void AcceptsEveryLetterInAChunkType(string type)
    {
        Assert.Equal(2, Parse(Gray(Chunk(type, 1))).Header.Width);
    }

    [Fact]
    public void IgnoresAnUnknownAncillaryChunk()
    {
        Assert.Equal(2, Parse(Gray(Chunk("cUST", 1, 2, 3), Chunk("prVT"))).Header.Height);
    }

    [Theory]
    [InlineData(1, 0x0000, 0)]
    [InlineData(1, 0x0001, 1)]
    [InlineData(4, 0x000F, 15)]
    [InlineData(8, 0x00FF, 255)]
    [InlineData(16, 0xFFFF, 65535)]
    public void ReadsTheTransparentGray(int bitDepth, int key, int expected)
    {
        PngFile png = Parse(Image(Header(0, bitDepth), Chunk("tRNS", TestPng.BigEndian16(key))));

        Assert.Equal(new[] { expected }, png.TransparentColor);
        Assert.True(png.PaletteAlpha.IsEmpty);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 16)]
    [InlineData(8, 256)]
    public void IgnoresATransparentGrayTheBitDepthCannotHold(int bitDepth, int key)
    {
        Assert.Null(Parse(Image(Header(0, bitDepth), Chunk("tRNS", TestPng.BigEndian16(key)))).TransparentColor);
    }

    [Fact]
    public void ReadsTheTransparentColour()
    {
        PngFile png = Parse(Image(Header(2, 16), Chunk("tRNS", TestPng.BigEndian16(1, 0x1234, 0xFFFF))));

        Assert.Equal(new[] { 1, 0x1234, 0xFFFF }, png.TransparentColor);
    }

    [Theory]
    [InlineData(0x100, 0, 0)]
    [InlineData(0, 0x100, 0)]
    [InlineData(0, 0, 0x100)]
    public void IgnoresATransparentColourTheBitDepthCannotHold(int red, int green, int blue)
    {
        Assert.Null(Parse(Image(Header(2, 8), Chunk("tRNS", TestPng.BigEndian16(red, green, blue)))).TransparentColor);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 8)]
    public void IgnoresATransparentColourOfTheWrongLength(int colorType, int length)
    {
        Assert.Null(Parse(Image(Header(colorType, 8), Chunk("tRNS", new byte[length]))).TransparentColor);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(4, 6)]
    [InlineData(6, 2)]
    [InlineData(6, 6)]
    public void IgnoresTransparencyInAnImageWithAlpha(int colorType, int length)
    {
        PngFile png = Parse(Image(Header(colorType, 8), Chunk("tRNS", new byte[length])));

        Assert.Null(png.TransparentColor);
        Assert.True(png.PaletteAlpha.IsEmpty);
    }

    [Fact]
    public void KeepsTheFirstTransparencyChunk()
    {
        PngFile png = Parse(Image(Header(0, 8), Chunk("tRNS", 0, 7), Chunk("tRNS", 0, 9)));

        Assert.Equal(new[] { 7 }, png.TransparentColor);
    }

    [Fact]
    public void ReadsPaletteAlpha()
    {
        PngFile png = TestImageFiles.Png("tm3n3p02.png");

        Assert.Equal(new byte[] { 0x00, 0x55, 0xAA }, png.PaletteAlpha.ToArray());
        Assert.Null(png.TransparentColor);
    }

    [Fact]
    public void TruncatesPaletteAlphaToThePalette()
    {
        PngFile png = Parse(Image(Header(3, 8), Palette(2), Chunk("tRNS", 1, 2, 3, 4)));

        Assert.Equal(new byte[] { 1, 2 }, png.PaletteAlpha.ToArray());
    }

    [Fact]
    public void KeepsPaletteAlphaAsLongAsThePalette()
    {
        PngFile png = Parse(Image(Header(3, 8), Palette(2), Chunk("tRNS", 1, 2)));

        Assert.Equal(new byte[] { 1, 2 }, png.PaletteAlpha.ToArray());
    }

    [Fact]
    public void ReadsGamma()
    {
        Assert.Equal(0.35, TestImageFiles.Png("g03n0g16.png").Metadata.Gamma);
        Assert.Equal(1.0, TestImageFiles.Png("basn0g08.png").Metadata.Gamma);
        Assert.Null(TestImageFiles.Png("z00n2c08.png").Metadata.Gamma);
    }

    [Fact]
    public void KeepsTheFirstValidGamma()
    {
        Assert.Equal(0.45455, Parse(Gray(Chunk("gAMA", Fixed(45455)), Chunk("gAMA", Fixed(100000)))).Metadata.Gamma);
        Assert.Equal(0.5, Parse(Gray(Chunk("gAMA", Fixed(0)), Chunk("gAMA", Fixed(50000)))).Metadata.Gamma);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void IgnoresGammaOfTheWrongLength(int length)
    {
        Assert.Null(Parse(Gray(Chunk("gAMA", new byte[length]))).Metadata.Gamma);
    }

    [Fact]
    public void ReadsChromaticities()
    {
        Chromaticities? chromaticities = TestImageFiles.Png("ccwn2c08.png").Metadata.Chromaticities;

        Assert.NotNull(chromaticities);
        Assert.Equal(0.3127, chromaticities.WhiteX);
        Assert.Equal(0.329, chromaticities.WhiteY);
        Assert.Equal(0.64, chromaticities.RedX);
        Assert.Equal(0.33, chromaticities.RedY);
        Assert.Equal(0.3, chromaticities.GreenX);
        Assert.Equal(0.6, chromaticities.GreenY);
        Assert.Equal(0.15, chromaticities.BlueX);
        Assert.Equal(0.06, chromaticities.BlueY);
    }

    [Fact]
    public void KeepsTheFirstValidChromaticities()
    {
        byte[] first = Fixed(1, 2, 3, 4, 5, 6, 7, 8);
        byte[] second = Fixed(9, 9, 9, 9, 9, 9, 9, 9);

        Assert.Equal(0.00001, Parse(Gray(Chunk("cHRM", first), Chunk("cHRM", second))).Metadata.Chromaticities!.WhiteX);
        Assert.Equal(0.00009, Parse(Gray(Chunk("cHRM", new byte[31]), Chunk("cHRM", second))).Metadata.Chromaticities!.BlueY);
        Assert.Null(Parse(Gray(Chunk("cHRM", new byte[33]))).Metadata.Chromaticities);
    }

    [Theory]
    [InlineData(0, (int)RenderingIntent.Perceptual)]
    [InlineData(1, (int)RenderingIntent.RelativeColorimetric)]
    [InlineData(2, (int)RenderingIntent.Saturation)]
    [InlineData(3, (int)RenderingIntent.AbsoluteColorimetric)]
    public void ReadsTheSrgbRenderingIntent(byte value, int expected)
    {
        Assert.Equal((RenderingIntent)expected, Parse(Gray(Chunk("sRGB", value))).Metadata.RenderingIntent);
    }

    [Fact]
    public void IgnoresAnInvalidSrgbChunk()
    {
        Assert.Null(Parse(Gray(Chunk("sRGB", 4))).Metadata.RenderingIntent);
        Assert.Null(Parse(Gray(Chunk("sRGB", 0, 0))).Metadata.RenderingIntent);
        Assert.Null(Parse(Gray(Chunk("sRGB"))).Metadata.RenderingIntent);
        Assert.Equal(RenderingIntent.Saturation, Parse(Gray(Chunk("sRGB", 9), Chunk("sRGB", 2))).Metadata.RenderingIntent);
        Assert.Equal(RenderingIntent.Perceptual, Parse(Gray(Chunk("sRGB", 0), Chunk("sRGB", 2))).Metadata.RenderingIntent);
        Assert.Null(TestImageFiles.Png("basn2c08.png").Metadata.RenderingIntent);
    }

    [Fact]
    public void ReadsTheOrientationFromAnExifChunk()
    {
        Assert.Equal(ExifOrientation.Normal, TestImageFiles.Png("exif2c08.png").Metadata.Orientation);
        Assert.Equal(ExifOrientation.Rotate270, Parse(Gray(Chunk("eXIf", TestJpeg.Orientation(8, true)))).Metadata.Orientation);
        Assert.Equal(ExifOrientation.Transverse, Parse(Gray(
            Chunk("eXIf", TestJpeg.Orientation(7)),
            Chunk("eXIf", TestJpeg.Orientation(2)))).Metadata.Orientation);
        Assert.Equal(ExifOrientation.Normal, Parse(Gray()).Metadata.Orientation);
    }

    // iCCP: name, terminator, compression method 0, zlib-compressed profile.
    private static byte[] ColorProfile(byte[] profile, string name = "ICC profile", byte method = 0, byte[]? compressed = null) =>
        Chunk("iCCP", [.. Encoding.ASCII.GetBytes(name), 0, method, .. compressed ?? TestZlib.Compress(profile)]);

    [Fact]
    public void ExtractsAnIccProfile()
    {
        IccProfile? profile = TestImageFiles.Png("png-iccp.png").Metadata.IccProfile;

        Assert.NotNull(profile);
        Assert.Equal(588, profile.Data.Length);
        Assert.Equal(3, profile.ComponentCount);
        Assert.Equal((byte)'a', profile.Data.Span[36]);
    }

    [Theory]
    [InlineData(0, "GRAY")]
    [InlineData(4, "GRAY")]
    [InlineData(2, "RGB ")]
    [InlineData(6, "RGB ")]
    [InlineData(3, "RGB ")]
    public void AcceptsTheProfileColourSpaceEachColourTypeNeeds(int colorType, string colorSpace)
    {
        byte[] profile = TestJpeg.Profile(colorSpace);
        byte[][] chunks = colorType == 3 ? [ColorProfile(profile), Palette(2)] : [ColorProfile(profile)];

        IccProfile? read = Parse(Image(Header(colorType, 8), chunks)).Metadata.IccProfile;

        Assert.NotNull(read);
        Assert.Equal(profile, read.Data.ToArray());
    }

    [Theory]
    [InlineData(0, "RGB ")]
    [InlineData(2, "GRAY")]
    [InlineData(2, "CMYK")]
    public void DropsAProfileForTheWrongColourSpace(int colorType, string colorSpace)
    {
        Assert.Null(Parse(Image(Header(colorType, 8), ColorProfile(TestJpeg.Profile(colorSpace)))).Metadata.IccProfile);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(79)]
    public void AcceptsProfileNamesOfOneTo79Bytes(int length)
    {
        byte[] chunk = ColorProfile(TestJpeg.Profile("GRAY"), new string('n', length));

        Assert.NotNull(Parse(Gray(chunk)).Metadata.IccProfile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    public void DropsAProfileWithAnInvalidName(int length)
    {
        byte[] chunk = ColorProfile(TestJpeg.Profile("GRAY"), new string('n', length));

        Assert.Null(Parse(Gray(chunk)).Metadata.IccProfile);
    }

    [Fact]
    public void DropsAProfileWithoutANameTerminator()
    {
        Assert.Null(Parse(Gray(Chunk("iCCP", Encoding.ASCII.GetBytes("ICC profile")))).Metadata.IccProfile);
    }

    [Fact]
    public void DropsAProfileEndingAtItsCompressionMethod()
    {
        Assert.Null(Parse(Gray(Chunk("iCCP", [.. "name"u8, 0]))).Metadata.IccProfile);
        Assert.Null(Parse(Gray(Chunk("iCCP", [.. "name"u8, 0, 0]))).Metadata.IccProfile);
    }

    [Fact]
    public void DropsAProfileWithAnUnknownCompressionMethod()
    {
        Assert.Null(Parse(Gray(ColorProfile(TestJpeg.Profile("GRAY"), method: 1))).Metadata.IccProfile);
    }

    [Fact]
    public void DropsAProfileThatDoesNotDecompress()
    {
        byte[] compressed = TestZlib.Compress(TestJpeg.Profile("GRAY"));
        compressed[compressed.Length - 1] ^= 0xFF;

        Assert.Null(Parse(Gray(ColorProfile([], compressed: compressed))).Metadata.IccProfile);
    }

    [Fact]
    public void DropsAProfileThatDecompressesBeyondTheLimit()
    {
        byte[] huge = TestJpeg.Profile("GRAY", size: (16 << 20) + 1);

        Assert.Null(Parse(Gray(ColorProfile(huge))).Metadata.IccProfile);
    }

    [Fact]
    public void AcceptsAProfileAtTheLimit()
    {
        byte[] large = TestJpeg.Profile("GRAY", size: 16 << 20);

        Assert.Equal(16 << 20, Parse(Gray(ColorProfile(large))).Metadata.IccProfile!.Data.Length);
    }

    [Fact]
    public void KeepsTheFirstProfileChunk()
    {
        byte[] first = TestJpeg.Profile("GRAY", size: 150);
        byte[] second = TestJpeg.Profile("GRAY", size: 160);

        Assert.Equal(150, Parse(Gray(ColorProfile(first), ColorProfile(second))).Metadata.IccProfile!.Data.Length);
    }
}
