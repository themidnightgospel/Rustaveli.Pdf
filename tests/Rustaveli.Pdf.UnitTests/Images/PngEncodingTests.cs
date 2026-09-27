using System.Runtime.InteropServices;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// What a PNG becomes in a PDF. Outputs are read back with a separate, naive inflate-and-unpredict and compared
/// with the rows the decoder produced, which <see cref="PngDecodeTests"/> pins to a reference decode.
/// </summary>
public class PngEncodingTests
{
    private static EncodedImage Encode(string name) => PngImageEncoder.Encode(TestImageFiles.Png(name));

    private static EncodedImage Encode(byte[] png) => PngImageEncoder.Encode(PngParser.Parse(png));

    private static byte[][] ReferenceRows(string name) => RowCollector.Decode(TestImageFiles.Png(name)).Rows.ToArray();

    /// <summary>The image data, inflated and unpredicted, one array per row.</summary>
    private static byte[][] Rows(EncodedImage image)
    {
        FlateDecodeParameters parameters = image.DecodeParameters!;
        return TestZlib.Unpredict(TestZlib.Decompress(image.Data), parameters.Colors, parameters.BitsPerComponent, parameters.Columns);
    }

    private static byte[] FilterTypes(EncodedImage image)
    {
        FlateDecodeParameters parameters = image.DecodeParameters!;
        int rowBytes = ((parameters.Columns * parameters.Colors * parameters.BitsPerComponent) + 7) / 8;
        return TestZlib.RowFilters(TestZlib.Decompress(image.Data), rowBytes);
    }

    // The concatenated IDAT payloads, found by walking the chunks here rather than by the parser under test.
    private static byte[] ImageData(byte[] file)
    {
        List<byte> data = [];
        int position = 8;
        while (position < file.Length)
        {
            int length = (int)TestPng.ReadUInt32(file, position);
            if (file[position + 4] == 'I' && file[position + 5] == 'D' && file[position + 6] == 'A' && file[position + 7] == 'T')
                data.AddRange(file.AsSpan(position + 8, length).ToArray());
            position += length + 12;
        }

        return data.ToArray();
    }

    // Splits interleaved rows into colour and alpha, sample by sample.
    private static (byte[][] Color, byte[][] Alpha) Split(byte[][] rows, int colorBytes, int alphaBytes)
    {
        int pixelBytes = colorBytes + alphaBytes;
        byte[][] color = rows.Select(row => Enumerable.Range(0, row.Length / pixelBytes)
            .SelectMany(pixel => row.Skip(pixel * pixelBytes).Take(colorBytes)).ToArray()).ToArray();
        byte[][] alpha = rows.Select(row => Enumerable.Range(0, row.Length / pixelBytes)
            .SelectMany(pixel => row.Skip((pixel * pixelBytes) + colorBytes).Take(alphaBytes)).ToArray()).ToArray();
        return (color, alpha);
    }

    [Theory]
    [InlineData("basn0g01.png", 1, 1)]
    [InlineData("basn0g02.png", 1, 2)]
    [InlineData("basn0g04.png", 1, 4)]
    [InlineData("basn0g08.png", 1, 8)]
    [InlineData("basn0g16.png", 1, 16)]
    [InlineData("basn2c08.png", 3, 8)]
    [InlineData("basn2c16.png", 3, 16)]
    [InlineData("basn3p01.png", 1, 1)]
    [InlineData("basn3p02.png", 1, 2)]
    [InlineData("basn3p04.png", 1, 4)]
    [InlineData("basn3p08.png", 1, 8)]
    [InlineData("f99n0g04.png", 1, 4)]
    [InlineData("oi4n2c16.png", 3, 16)]
    [InlineData("oi9n0g16.png", 1, 16)]
    [InlineData("z00n2c08.png", 3, 8)]
    [InlineData("png-iccp.png", 3, 8)]
    public void PassesTheImageDataOfAnOpaqueImageStraightThrough(string name, int colors, int bitDepth)
    {
        byte[] file = TestImageFiles.Bytes(name);
        PngHeader header = PngParser.Parse(file).Header;

        EncodedImage image = Encode(file);

        Assert.Equal(ImageData(file), image.Data.ToArray());
        Assert.Equal(ImageFilter.Flate, image.Filter);
        Assert.Equal(header.Width, image.Width);
        Assert.Equal(header.Height, image.Height);
        Assert.Equal(bitDepth, image.BitsPerComponent);
        Assert.Equal(15, image.DecodeParameters!.Predictor);
        Assert.Equal(colors, image.DecodeParameters.Colors);
        Assert.Equal(bitDepth, image.DecodeParameters.BitsPerComponent);
        Assert.Equal(header.Width, image.DecodeParameters.Columns);
        Assert.Null(image.SoftMask);
        Assert.Null(image.ColorKeyMask);
        Assert.Null(image.Decode);
        Assert.Null(image.ColorTransform);
        Assert.Equal(ReferenceRows(name), Rows(image));
    }

    [Fact]
    public void NeverInflatesImageDataItPassesThrough()
    {
        // Not a deflate stream at all, but with a correct CRC: only code that never inflates can accept it, and it
        // must come out untouched.
        byte[] garbage = Enumerable.Range(0, 300).Select(index => (byte)((index * 97) + 13)).ToArray();
        byte[] opaque = TestPng.Build(TestPng.Header(40, 30, 8, 2), TestPng.Data(garbage), TestPng.End());

        EncodedImage image = Encode(opaque);

        Assert.Equal(garbage, image.Data.ToArray());
    }

    [Fact]
    public void NeverInflatesImageDataSplitAcrossChunks()
    {
        byte[] first = Enumerable.Range(0, 100).Select(index => (byte)(index * 3)).ToArray();
        byte[] second = Enumerable.Range(0, 50).Select(index => (byte)(index * 5)).ToArray();
        byte[] palette = TestPng.Chunk("PLTE", new byte[12]);
        byte[] file = TestPng.Build(TestPng.Header(40, 30, 2, 3), palette, TestPng.Data(first), TestPng.Data(second), TestPng.End());

        EncodedImage image = Encode(file);

        Assert.Equal([.. first, .. second], image.Data.ToArray());
    }

    [Fact]
    public void DecodesWhatItCannotPassThrough()
    {
        // The same garbage, interlaced: this image has to be decoded, and the decoder rejects it.
        byte[] garbage = Enumerable.Range(0, 300).Select(index => (byte)((index * 97) + 13)).ToArray();
        byte[] interlaced = TestPng.Build(TestPng.Header(40, 30, 8, 2, interlace: 1), TestPng.Data(garbage), TestPng.End());

        Assert.Throws<ImageFormatException>(() => Encode(interlaced));
    }

    [Fact]
    public void PassesASingleIdatPayloadThroughWithoutCopyingIt()
    {
        byte[] file = TestImageFiles.Bytes("basn2c08.png");

        EncodedImage image = Encode(file);

        Assert.True(MemoryMarshal.TryGetArray(image.Data, out ArraySegment<byte> segment));
        Assert.Same(file, segment.Array);
    }

    [Theory]
    [InlineData("basn0g08.png", (int)ImageColorSpaceKind.DeviceGray)]
    [InlineData("basn0g16.png", (int)ImageColorSpaceKind.DeviceGray)]
    [InlineData("basn2c08.png", (int)ImageColorSpaceKind.DeviceRgb)]
    [InlineData("basn4a08.png", (int)ImageColorSpaceKind.DeviceGray)]
    [InlineData("basn6a16.png", (int)ImageColorSpaceKind.DeviceRgb)]
    public void UsesTheDeviceSpaceOfTheColourType(string name, int kind)
    {
        Assert.Equal((ImageColorSpaceKind)kind, Encode(name).ColorSpace.Kind);
    }

    [Fact]
    public void DescribesAPaletteAsAnIndexedSpaceOverRgb()
    {
        EncodedImage image = Encode("basn3p02.png");

        Assert.Equal(ImageColorSpaceKind.Indexed, image.ColorSpace.Kind);
        Assert.Same(ImageColorSpace.DeviceRgb, image.ColorSpace.Base);
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x00, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF }, image.ColorSpace.Palette.ToArray());
        Assert.Equal(3, image.ColorSpace.HighValue);
        Assert.Equal(1, image.DecodeParameters!.Colors);
    }

    [Fact]
    public void DescribesAnImageWithAProfileInAnIccBasedSpace()
    {
        PngFile png = TestImageFiles.Png("png-iccp.png");

        EncodedImage image = PngImageEncoder.Encode(png);

        Assert.Equal(ImageColorSpaceKind.IccBased, image.ColorSpace.Kind);
        Assert.Same(png.Metadata.IccProfile, image.ColorSpace.Profile);
        Assert.Equal(3, image.ColorSpace.ComponentCount);
    }

    [Fact]
    public void DescribesAGrayImageWithAProfileInAOneComponentIccBasedSpace()
    {
        byte[] profile = TestJpeg.Profile("GRAY");
        byte[] iccp = TestPng.Chunk("iCCP", [.. "gray"u8, 0, 0, .. TestZlib.Compress(profile)]);

        EncodedImage image = Encode(TestPng.Image(2, 2, 8, 0, new byte[6], false, iccp));

        Assert.Equal(ImageColorSpaceKind.IccBased, image.ColorSpace.Kind);
        Assert.Equal(1, image.ColorSpace.Profile!.ComponentCount);
        Assert.Same(ImageColorSpace.DeviceGray, image.ColorSpace.Alternate);
    }

    [Fact]
    public void BasesAPaletteWithAProfileOnTheProfile()
    {
        byte[] iccp = TestPng.Chunk("iCCP", [.. "rgb"u8, 0, 0, .. TestZlib.Compress(TestJpeg.Profile("RGB "))]);
        byte[] palette = TestPng.Chunk("PLTE", [1, 2, 3, 4, 5, 6]);

        EncodedImage image = Encode(TestPng.Image(2, 2, 1, 3, new byte[4], false, iccp, palette));

        Assert.Equal(ImageColorSpaceKind.Indexed, image.ColorSpace.Kind);
        Assert.Equal(ImageColorSpaceKind.IccBased, image.ColorSpace.Base!.Kind);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, image.ColorSpace.Palette.ToArray());
        Assert.Equal(1, image.ColorSpace.HighValue);
    }

    [Theory]
    [InlineData("tbbn0g04.png", new[] { 15, 15 })]
    [InlineData("tbwn0g16.png", new[] { 65535, 65535 })]
    [InlineData("tbrn2c08.png", new[] { 255, 255, 255, 255, 255, 255 })]
    [InlineData("tbbn2c16.png", new[] { 65535, 65535, 65535, 65535, 65535, 65535 })]
    public void MasksATransparentColourWithAnExactColourKey(string name, int[] ranges)
    {
        byte[] file = TestImageFiles.Bytes(name);

        EncodedImage image = Encode(file);

        Assert.Equal(ranges, image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(ImageData(file), image.Data.ToArray());
    }

    [Fact]
    public void MasksTheColourKeyOfEachChannel()
    {
        byte[] trns = TestPng.Chunk("tRNS", TestPng.BigEndian16(1, 2, 3));

        EncodedImage image = Encode(TestPng.Image(1, 1, 8, 2, [0, 1, 2, 3], false, trns));

        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, image.ColorKeyMask);
    }

    [Theory]
    [InlineData("tbbn3p08.png")]
    [InlineData("tp1n3p08.png")]
    public void MasksASingleTransparentPaletteEntryWithAColourKey(string name)
    {
        byte[] file = TestImageFiles.Bytes(name);

        EncodedImage image = Encode(file);

        Assert.Equal(new[] { 0, 0 }, image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(ImageData(file), image.Data.ToArray());
    }

    // A 4 × 1 image of palette indices 0, 1, 2 and 3 at 2 bits, with four palette entries and the given alpha.
    // Interlaced, pass 1 holds x = 0, pass 4 x = 2 and pass 6 x = 1 and 3; the other passes are empty.
    private static byte[] PaletteImage(byte[] alpha, bool interlaced = false)
    {
        byte[] palette = TestPng.Chunk("PLTE", Enumerable.Range(0, 12).Select(index => (byte)(index * 20)).ToArray());
        byte[] filtered = interlaced ? [0, 0x00, 0, 0x80, 0, 0x70] : [0, 0x1B];
        byte[][] chunks = alpha.Length == 0 ? [palette] : [palette, TestPng.Chunk("tRNS", alpha)];
        return TestPng.Image(4, 1, 2, 3, filtered, interlaced, chunks);
    }

    [Fact]
    public void LeavesAPaletteWhoseAlphaIsAllOpaqueUnmasked()
    {
        byte[] file = PaletteImage([255, 255, 255]);

        EncodedImage image = Encode(file);

        Assert.Null(image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(ImageData(file), image.Data.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { 0 }, 0, 0)]
    [InlineData(new byte[] { 255, 0 }, 1, 1)]
    [InlineData(new byte[] { 255, 0, 0 }, 1, 2)]
    [InlineData(new byte[] { 0, 0, 0, 0 }, 0, 3)]
    [InlineData(new byte[] { 255, 255, 255, 0 }, 3, 3)]
    public void MasksAContiguousRunOfTransparentEntriesWithAColourKey(byte[] alpha, int first, int last)
    {
        byte[] file = PaletteImage(alpha);

        EncodedImage image = Encode(file);

        Assert.Equal(new[] { first, last }, image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(ImageData(file), image.Data.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { 0, 255, 0 }, new byte[] { 0, 255, 0, 255 })]
    [InlineData(new byte[] { 0, 0, 255, 0 }, new byte[] { 0, 0, 255, 0 })]
    [InlineData(new byte[] { 255, 128 }, new byte[] { 255, 128, 255, 255 })]
    [InlineData(new byte[] { 0, 1, 254 }, new byte[] { 0, 1, 254, 255 })]
    [InlineData(new byte[] { 255, 255, 255, 7 }, new byte[] { 255, 255, 255, 7 })]
    public void GivesAnyOtherPaletteAlphaASoftMask(byte[] alpha, byte[] expected)
    {
        EncodedImage image = Encode(PaletteImage(alpha));

        Assert.Null(image.ColorKeyMask);
        Assert.NotNull(image.SoftMask);
        Assert.Equal(new[] { expected }, Rows(image.SoftMask));
        Assert.Equal(new[] { new byte[] { 0x1B } }, Rows(image));
    }

    [Fact]
    public void TreatsIndicesBeyondThePaletteAsOpaque()
    {
        // Indices 2 and 3 have no tRNS entry, and 3 has no palette entry either: both are opaque.
        byte[] palette = TestPng.Chunk("PLTE", new byte[9]);
        byte[] trns = TestPng.Chunk("tRNS", [10, 20]);

        EncodedImage image = Encode(TestPng.Image(4, 1, 2, 3, [0, 0x1B], false, palette, trns));

        Assert.Equal(new[] { new byte[] { 10, 20, 255, 255 } }, Rows(image.SoftMask!));
    }

    [Fact]
    public void BuildsTheSoftMaskOfAPaletteWithSeveralAlphaLevels()
    {
        EncodedImage image = Encode("tm3n3p02.png");
        EncodedImage mask = image.SoftMask!;

        Assert.Equal(ImageColorSpaceKind.Indexed, image.ColorSpace.Kind);
        Assert.Equal(2, image.BitsPerComponent);
        Assert.Equal(ReferenceRows("tm3n3p02.png"), Rows(image));
        Assert.All(FilterTypes(image), filter => Assert.Equal(PngFilters.None, filter));

        Assert.Same(ImageColorSpace.DeviceGray, mask.ColorSpace);
        Assert.Equal(8, mask.BitsPerComponent);
        Assert.Equal(32, mask.Width);
        Assert.Equal(32, mask.Height);
        Assert.Equal(ImageFilter.Flate, mask.Filter);
        Assert.Equal(1, mask.DecodeParameters!.Colors);
        Assert.Equal(8, mask.DecodeParameters.BitsPerComponent);
        Assert.Equal(32, mask.DecodeParameters.Columns);
        Assert.Null(mask.SoftMask);
        Assert.Null(mask.ColorKeyMask);

        byte[][] alpha = Rows(mask);
        Assert.Equal([.. Enumerable.Repeat((byte)0x00, 16), .. Enumerable.Repeat((byte)0x55, 16)], alpha[0]);
        Assert.Equal([.. Enumerable.Repeat((byte)0xAA, 16), .. Enumerable.Repeat((byte)0xFF, 16)], alpha[16]);
        Assert.Equal(alpha[16], alpha[31]);
        Assert.All(FilterTypes(mask), filter => Assert.Equal(PngFilters.Paeth, filter));
    }

    [Theory]
    [InlineData("basn4a08.png", 1, 1)]
    [InlineData("basn4a16.png", 2, 2)]
    [InlineData("basn6a08.png", 3, 1)]
    [InlineData("basn6a16.png", 6, 2)]
    [InlineData("basi4a08.png", 1, 1)]
    [InlineData("basi4a16.png", 2, 2)]
    [InlineData("basi6a08.png", 3, 1)]
    [InlineData("basi6a16.png", 6, 2)]
    public void SeparatesAnAlphaChannelIntoASoftMask(string name, int colorBytes, int alphaBytes)
    {
        (byte[][] color, byte[][] alpha) = Split(ReferenceRows(name), colorBytes, alphaBytes);
        int bitDepth = alphaBytes * 8;

        EncodedImage image = Encode(name);
        EncodedImage mask = image.SoftMask!;

        Assert.Equal(color, Rows(image));
        Assert.Equal(bitDepth, image.BitsPerComponent);
        Assert.Equal(colorBytes / alphaBytes, image.DecodeParameters!.Colors);
        Assert.Equal(bitDepth, image.DecodeParameters.BitsPerComponent);
        Assert.Null(image.ColorKeyMask);

        Assert.Equal(alpha, Rows(mask));
        Assert.Same(ImageColorSpace.DeviceGray, mask.ColorSpace);
        Assert.Equal(bitDepth, mask.BitsPerComponent);
        Assert.Equal(1, mask.DecodeParameters!.Colors);
        Assert.Equal(bitDepth, mask.DecodeParameters.BitsPerComponent);
        Assert.Equal(32, mask.DecodeParameters.Columns);
        Assert.Equal(32, mask.Width);
        Assert.Equal(32, mask.Height);

        Assert.All(FilterTypes(image), filter => Assert.Equal(PngFilters.Paeth, filter));
        Assert.All(FilterTypes(mask), filter => Assert.Equal(PngFilters.Paeth, filter));
    }

    [Theory]
    [InlineData(0, 0, 0x00)]
    [InlineData(31, 0, 0xFF)]
    [InlineData(0, 31, 0x00)]
    [InlineData(31, 31, 0xFF)]
    [InlineData(16, 8, 0x83)]
    [InlineData(5, 20, 0x29)]
    public void KeepsTheAlphaOfEachPixel(int x, int y, byte alpha)
    {
        Assert.Equal(alpha, Rows(Encode("basn6a08.png").SoftMask!)[y][x]);
        Assert.Equal(alpha, Rows(Encode("basi6a08.png").SoftMask!)[y][x]);
    }

    [Fact]
    public void KeepsSixteenBitColourAndAlpha()
    {
        EncodedImage gray = Encode("basn4a16.png");
        EncodedImage rgb = Encode("basn6a16.png");

        Assert.Equal(new byte[] { 0xEE, 0xED }, Rows(gray)[8].AsSpan(32, 2).ToArray());
        Assert.Equal(new byte[] { 0x84, 0x21 }, Rows(gray.SoftMask!)[8].AsSpan(32, 2).ToArray());
        Assert.Equal(new byte[] { 0x00, 0x00 }, Rows(gray.SoftMask!)[0].AsSpan(0, 2).ToArray());
        Assert.Equal(new byte[] { 0x77, 0x76, 0xFF, 0xFF, 0x00, 0x00 }, Rows(rgb)[8].AsSpan(96, 6).ToArray());
        Assert.Equal(new byte[] { 0x84, 0x21 }, Rows(rgb.SoftMask!)[8].AsSpan(32, 2).ToArray());
    }

    [Theory]
    [InlineData("basi0g01.png", 1, 1)]
    [InlineData("basi0g04.png", 1, 4)]
    [InlineData("basi0g16.png", 1, 16)]
    [InlineData("basi2c08.png", 3, 8)]
    [InlineData("basi2c16.png", 3, 16)]
    [InlineData("basi3p02.png", 1, 2)]
    [InlineData("basi3p08.png", 1, 8)]
    [InlineData("s09i3p02.png", 1, 2)]
    public void DeinterlacesAnOpaqueImage(string name, int colors, int bitDepth)
    {
        byte[] file = TestImageFiles.Bytes(name);
        string sequential = name.Replace("basi", "basn").Replace("s09i", "s09n");

        EncodedImage image = Encode(file);

        Assert.NotEqual(ImageData(file), image.Data.ToArray());
        Assert.Equal(ReferenceRows(sequential), Rows(image));
        Assert.Equal(colors, image.DecodeParameters!.Colors);
        Assert.Equal(bitDepth, image.BitsPerComponent);
        Assert.Null(image.SoftMask);
        Assert.Null(image.ColorKeyMask);

        byte expectedFilter = bitDepth < 8 ? PngFilters.None : PngFilters.Paeth;
        Assert.All(FilterTypes(image), filter => Assert.Equal(expectedFilter, filter));
    }

    [Fact]
    public void KeepsTheColourKeyOfAnInterlacedImage()
    {
        byte[] trns = TestPng.Chunk("tRNS", TestPng.BigEndian16(3));

        EncodedImage image = Encode(TestPng.Image(1, 1, 4, 0, [0, 0x30], true, trns));

        Assert.Equal(new[] { 3, 3 }, image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(new[] { new byte[] { 0x30 } }, Rows(image));
    }

    [Fact]
    public void KeepsThePaletteColourKeyOfAnInterlacedImage()
    {
        EncodedImage image = Encode(PaletteImage([255, 0], interlaced: true));

        Assert.Equal(new[] { 1, 1 }, image.ColorKeyMask);
        Assert.Null(image.SoftMask);
        Assert.Equal(new[] { new byte[] { 0x1B } }, Rows(image));
    }

    [Fact]
    public void GivesAnInterlacedPaletteASoftMask()
    {
        EncodedImage image = Encode(PaletteImage([0, 100], interlaced: true));

        Assert.Equal(new[] { new byte[] { 0, 100, 255, 255 } }, Rows(image.SoftMask!));
        Assert.Equal(new[] { new byte[] { 0x1B } }, Rows(image));
    }

    [Theory]
    [InlineData(4, 8)]
    [InlineData(4, 16)]
    [InlineData(6, 8)]
    [InlineData(6, 16)]
    public void DropsAnAlphaChannelThatIsEntirelyOpaque(int colorType, int bitDepth)
    {
        PngHeader header = new PngHeader(3, 2, bitDepth, (PngColorType)colorType, false);
        int sampleBytes = bitDepth / 8;
        int pixelBytes = header.Channels * sampleBytes;
        byte[][] rows = Enumerable.Range(0, 2).Select(row => Enumerable.Range(0, 3 * pixelBytes)
            .Select(index => index % pixelBytes >= pixelBytes - sampleBytes ? (byte)0xFF : (byte)(index + row)).ToArray()).ToArray();

        EncodedImage image = Encode(TestPng.Image(3, 2, bitDepth, colorType, TestPng.Filter(rows, pixelBytes, 1)));

        Assert.Null(image.SoftMask);
        Assert.Equal(Split(rows, pixelBytes - sampleBytes, sampleBytes).Color, Rows(image));
    }

    [Theory]
    [InlineData(0xFF, 0xFE)]
    [InlineData(0xFE, 0xFF)]
    [InlineData(0x00, 0x00)]
    public void KeepsASixteenBitAlphaChannelThatIsNotQuiteOpaque(byte high, byte low)
    {
        byte[][] rows = [[1, 2, 0xFF, 0xFF, 3, 4, high, low], [5, 6, 0xFF, 0xFF, 7, 8, 0xFF, 0xFF]];

        EncodedImage image = Encode(TestPng.Image(2, 2, 16, 4, TestPng.Filter(rows, 4, 0)));

        Assert.Equal(new[] { new byte[] { 0xFF, 0xFF, high, low }, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } }, Rows(image.SoftMask!));
    }

    [Fact]
    public void KeepsAnAlphaChannelThatIsTranslucentOnlyInItsLastRow()
    {
        byte[][] rows = [[1, 255, 2, 255], [3, 255, 4, 255], [5, 255, 6, 254]];

        EncodedImage image = Encode(TestPng.Image(2, 3, 8, 4, TestPng.Filter(rows, 2, 0)));

        Assert.Equal(new[] { new byte[] { 255, 255 }, new byte[] { 255, 255 }, new byte[] { 255, 254 } }, Rows(image.SoftMask!));
        Assert.Equal(new[] { new byte[] { 1, 2 }, new byte[] { 3, 4 }, new byte[] { 5, 6 } }, Rows(image));
    }

    [Fact]
    public void KeepsAnAlphaChannelThatIsTranslucentOnlyInItsFirstRow()
    {
        byte[][] rows = [[1, 0, 2, 255], [3, 255, 4, 255]];

        EncodedImage image = Encode(TestPng.Image(2, 2, 8, 4, TestPng.Filter(rows, 2, 0)));

        Assert.Equal(new[] { new byte[] { 0, 255 }, new byte[] { 255, 255 } }, Rows(image.SoftMask!));
    }

    [Fact]
    public void ProducesTheSameOutputForAnInterlacedImageAsForItsSequentialTwin()
    {
        EncodedImage interlaced = Encode("basi6a08.png");
        EncodedImage sequential = Encode("basn6a08.png");

        Assert.Equal(sequential.Data.ToArray(), interlaced.Data.ToArray());
        Assert.Equal(sequential.SoftMask!.Data.ToArray(), interlaced.SoftMask!.Data.ToArray());
    }
}
