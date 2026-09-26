using System.Buffers;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// Unfiltering and de-interlacing, checked against a reference: the SHA-256 of each fixture's rows as decoded by an
/// independent implementation (Python's zlib and a straightforward unfilter, itself checked against Pillow).
/// </summary>
public class PngDecodeTests
{
    private static byte[][] Decode(byte[] png) => RowCollector.Decode(PngParser.Parse(png)).Rows.ToArray();

    private static string Fails(byte[] png) =>
        Assert.Throws<ImageFormatException>(() => RowCollector.Decode(PngParser.Parse(png))).Message;

    [Theory]
    [InlineData("basi0g01.png", "43a785714988ee3573c54e5d7b8d98d2add4969107d21ce2dade755bc486a368")]
    [InlineData("basi0g02.png", "a787061a3170b752e1290087f9a1337ca24f45464d0f4f811d9df04c7f15fe74")]
    [InlineData("basi0g04.png", "dfa3b06b3e297bd2683030b6467133e79f657b141a91a53a0198e14da1aed72e")]
    [InlineData("basi0g08.png", "3f79224ccb00156a58645afcd6521d0facbf9cdec212b03935eb25e59e9dc532")]
    [InlineData("basi0g16.png", "bd5ce54014a325deabcef479b7b62639f5bd651e00741eaaa1dd37a66091778c")]
    [InlineData("basi2c08.png", "3ff78c7d0ac9033c81fbcc389478d7a594ef5508979e1b6a63cfd5b7f1949beb")]
    [InlineData("basi2c16.png", "e2703f2e6722086d78e9f0da1d1dda2174f92bd7e27f45ae5177b282ec626eff")]
    [InlineData("basi3p01.png", "cb2e886e7f5035129309acb4d724e05d8a2928c3ff09061a6e746a9954a3f72f")]
    [InlineData("basi3p02.png", "a9753a1380a93576c329ffd7baa0de395519ae28f0f3a229e9746266e6e9be45")]
    [InlineData("basi3p04.png", "3d959587e025c4462461e49ffc7b592daf8d180bfe0e3cbe12913b41993c23b5")]
    [InlineData("basi3p08.png", "13a149ddd561daa99b0033e2f9aa5366c28ff11bbad9e555f8ab6a7f7acd8e02")]
    [InlineData("basi4a08.png", "699c411e440723b7857255cab5d47cc617e61f3511866d8745f50fbcc24535e9")]
    [InlineData("basi4a16.png", "efbbc333bdd49dec3f802d1f68ea1626a2300109809996ce4c0daa4696a46079")]
    [InlineData("basi6a08.png", "2eb6a2cb3166e9c188add371157e9f81caa18fdf34d218844ed930b53b7431d2")]
    [InlineData("basi6a16.png", "165b1f18ae3a6b43badb788ea6ee9040d4fcf1d47ee28ee66c48e36f6a52768b")]
    [InlineData("basn0g01.png", "43a785714988ee3573c54e5d7b8d98d2add4969107d21ce2dade755bc486a368")]
    [InlineData("basn0g02.png", "a787061a3170b752e1290087f9a1337ca24f45464d0f4f811d9df04c7f15fe74")]
    [InlineData("basn0g04.png", "dfa3b06b3e297bd2683030b6467133e79f657b141a91a53a0198e14da1aed72e")]
    [InlineData("basn0g08.png", "3f79224ccb00156a58645afcd6521d0facbf9cdec212b03935eb25e59e9dc532")]
    [InlineData("basn0g16.png", "bd5ce54014a325deabcef479b7b62639f5bd651e00741eaaa1dd37a66091778c")]
    [InlineData("basn2c08.png", "3ff78c7d0ac9033c81fbcc389478d7a594ef5508979e1b6a63cfd5b7f1949beb")]
    [InlineData("basn2c16.png", "e2703f2e6722086d78e9f0da1d1dda2174f92bd7e27f45ae5177b282ec626eff")]
    [InlineData("basn3p01.png", "cb2e886e7f5035129309acb4d724e05d8a2928c3ff09061a6e746a9954a3f72f")]
    [InlineData("basn3p02.png", "a9753a1380a93576c329ffd7baa0de395519ae28f0f3a229e9746266e6e9be45")]
    [InlineData("basn3p04.png", "3d959587e025c4462461e49ffc7b592daf8d180bfe0e3cbe12913b41993c23b5")]
    [InlineData("basn3p08.png", "13a149ddd561daa99b0033e2f9aa5366c28ff11bbad9e555f8ab6a7f7acd8e02")]
    [InlineData("basn4a08.png", "699c411e440723b7857255cab5d47cc617e61f3511866d8745f50fbcc24535e9")]
    [InlineData("basn4a16.png", "efbbc333bdd49dec3f802d1f68ea1626a2300109809996ce4c0daa4696a46079")]
    [InlineData("basn6a08.png", "2eb6a2cb3166e9c188add371157e9f81caa18fdf34d218844ed930b53b7431d2")]
    [InlineData("basn6a16.png", "165b1f18ae3a6b43badb788ea6ee9040d4fcf1d47ee28ee66c48e36f6a52768b")]
    [InlineData("ccwn2c08.png", "aa3f73251f6bbc2941340f7ec100cfdce91405f81b450f61f1988e34e34f4e31")]
    [InlineData("exif2c08.png", "e30c3d99987a4addf9d5c6dbdb818b9daa40266798d95730027f532b0af7c927")]
    [InlineData("f00n0g08.png", "7ba6cb6da925cf1aa5c00575ecaaba8e87689639d54cdc19aa1bdc7c7b851f6e")]
    [InlineData("f01n0g08.png", "6722cab2e71779b316ef8eda1314dc314e6c02b07e4896e5ed0cafe30284a927")]
    [InlineData("f02n0g08.png", "b188c36f926b7284ecc9981dd054aec56af32846b2fb4434260b7bba57c3a6da")]
    [InlineData("f03n0g08.png", "b1bf13e1d1d30fd33f3b4222e239ef3b2cc35a7db2143246bb7a20231fc3ee2e")]
    [InlineData("f04n0g08.png", "31dd33123e9c84b0ba4ac794bb1b4f93d84793e314ccd7b2d8f69e7993477a9e")]
    [InlineData("f99n0g04.png", "054bcec432f040d71cd3f68ee543fced428479facdef2bdbd6b5a1ac34806ca6")]
    [InlineData("g03n0g16.png", "f82d481560a346ab2020caac6dc33b9c126fb932bb076158dac6a9d96e83b301")]
    [InlineData("oi4n2c16.png", "e2703f2e6722086d78e9f0da1d1dda2174f92bd7e27f45ae5177b282ec626eff")]
    [InlineData("oi9n0g16.png", "bd5ce54014a325deabcef479b7b62639f5bd651e00741eaaa1dd37a66091778c")]
    [InlineData("png-iccp.png", "db523e9c34cedb1568701094d9570dbfcc0ab8826934f3a9a30a8e10521e2298")]
    [InlineData("s01i3p01.png", "6e340b9cffb37a989ca544e6bb780a2c78901d3fb33738768511a30617afa01d")]
    [InlineData("s01n3p01.png", "6e340b9cffb37a989ca544e6bb780a2c78901d3fb33738768511a30617afa01d")]
    [InlineData("s02i3p01.png", "96a296d224f285c67bee93c30f8a309157f0daa35dc5b87e410b78630a09cfc7")]
    [InlineData("s02n3p01.png", "96a296d224f285c67bee93c30f8a309157f0daa35dc5b87e410b78630a09cfc7")]
    [InlineData("s03i3p01.png", "8257c1dcf2dd679475a8b10db22f40210535e58cfc311aa3866e624414b009b0")]
    [InlineData("s03n3p01.png", "8257c1dcf2dd679475a8b10db22f40210535e58cfc311aa3866e624414b009b0")]
    [InlineData("s04i3p01.png", "14be24d98e00f0a0daa80c8bcbe9a77f8cd91b43daa61d914fcdf7612c51f59e")]
    [InlineData("s04n3p01.png", "14be24d98e00f0a0daa80c8bcbe9a77f8cd91b43daa61d914fcdf7612c51f59e")]
    [InlineData("s05i3p02.png", "f9c94cd5a1ef3b9416d9d24eb0a2728dd011b1f04b157736e64102de5d4408e1")]
    [InlineData("s05n3p02.png", "f9c94cd5a1ef3b9416d9d24eb0a2728dd011b1f04b157736e64102de5d4408e1")]
    [InlineData("s06i3p02.png", "bcdc98645a54b5fad5303b7f35eca21e671fe4a0ae3d5fdc55467607c41f1a55")]
    [InlineData("s06n3p02.png", "bcdc98645a54b5fad5303b7f35eca21e671fe4a0ae3d5fdc55467607c41f1a55")]
    [InlineData("s07i3p02.png", "f189ca480e61e14b352e478cf6964ffcc81f7aaf1f06a6363a93de825744768b")]
    [InlineData("s07n3p02.png", "f189ca480e61e14b352e478cf6964ffcc81f7aaf1f06a6363a93de825744768b")]
    [InlineData("s08i3p02.png", "fb75cfe8b949d6a883bf78c51da727385f296a47cb3ea88bc69931186abf53fd")]
    [InlineData("s08n3p02.png", "fb75cfe8b949d6a883bf78c51da727385f296a47cb3ea88bc69931186abf53fd")]
    [InlineData("s09i3p02.png", "88a82956cf148b634494c9cb4efd4cd82679a74ad611d5c03d247d93f66a4b1b")]
    [InlineData("s09n3p02.png", "88a82956cf148b634494c9cb4efd4cd82679a74ad611d5c03d247d93f66a4b1b")]
    [InlineData("tbbn0g04.png", "e7e084339a032c97bda06c4428aaf96d47980824b50ce258be9198579fa2a3ea")]
    [InlineData("tbbn2c16.png", "08dbb27d5a81bfefa6656ea43231c6ee20523b2c212228ff88005f80f2dfe742")]
    [InlineData("tbbn3p08.png", "696a923fe74f240d4d76495bc3413a39d0a1cd8d0708fa08fea26e4abb255a85")]
    [InlineData("tbrn2c08.png", "ebefb12e340b9af9e649bebb3506b17e89726a47af24cfa9fd03a918ee5599c3")]
    [InlineData("tbwn0g16.png", "da74b48f756bea084eddcd9f7a9f1726ae40cc37bd9038c97cafcb638a871052")]
    [InlineData("tm3n3p02.png", "a8b651773601c44f0ac337f761f04bf51ba077844e067869d91767ed987ce636")]
    [InlineData("tp1n3p08.png", "696a923fe74f240d4d76495bc3413a39d0a1cd8d0708fa08fea26e4abb255a85")]
    [InlineData("z00n2c08.png", "2d2e86be37826088a285f0420d94744c522bdb162202ab5ea5fc3c14a1fb3aae")]
    [InlineData("z09n2c08.png", "2d2e86be37826088a285f0420d94744c522bdb162202ab5ea5fc3c14a1fb3aae")]
    public void DecodesEveryFixtureExactly(string name, string sha256)
    {
        PngFile png = TestImageFiles.Png(name);

        RowCollector rows = RowCollector.Decode(png);

        Assert.Equal(png.Header.Height, rows.Rows.Count);
        Assert.All(rows.Rows, row => Assert.Equal(png.Header.RowBytes(png.Header.Width), row.Length));
        Assert.Equal(sha256, rows.Sha256());
    }

    [Theory]
    [InlineData("0g01")]
    [InlineData("0g02")]
    [InlineData("0g04")]
    [InlineData("0g08")]
    [InlineData("0g16")]
    [InlineData("2c08")]
    [InlineData("2c16")]
    [InlineData("3p01")]
    [InlineData("3p02")]
    [InlineData("3p04")]
    [InlineData("3p08")]
    [InlineData("4a08")]
    [InlineData("4a16")]
    [InlineData("6a08")]
    [InlineData("6a16")]
    public void DeinterlacesToTheSamePixelsAsTheNonInterlacedImage(string kind)
    {
        byte[][] interlaced = Decode(TestImageFiles.Bytes($"basi{kind}.png"));
        byte[][] sequential = Decode(TestImageFiles.Bytes($"basn{kind}.png"));

        Assert.Equal(sequential, interlaced);
    }

    [Theory]
    [InlineData("s01", "3p01")]
    [InlineData("s02", "3p01")]
    [InlineData("s03", "3p01")]
    [InlineData("s04", "3p01")]
    [InlineData("s05", "3p02")]
    [InlineData("s06", "3p02")]
    [InlineData("s07", "3p02")]
    [InlineData("s08", "3p02")]
    [InlineData("s09", "3p02")]
    public void DeinterlacesImagesTooSmallToReachEveryPass(string size, string kind)
    {
        Assert.Equal(Decode(TestImageFiles.Bytes($"{size}n{kind}.png")), Decode(TestImageFiles.Bytes($"{size}i{kind}.png")));
    }

    public static TheoryData<int, int, int> Layouts => new TheoryData<int, int, int>
    {
        // colour type, bit depth, width
        { 0, 1, 13 },
        { 0, 2, 7 },
        { 0, 4, 5 },
        { 0, 8, 6 },
        { 0, 16, 5 },
        { 2, 8, 4 },
        { 2, 16, 3 },
        { 3, 4, 9 },
        { 4, 8, 5 },
        { 4, 16, 4 },
        { 6, 8, 4 },
        { 6, 16, 3 },
    };

    private static byte[][] Pattern(int rows, int rowBytes, int seed) =>
        Enumerable.Range(0, rows)
            .Select(row => Enumerable.Range(0, rowBytes).Select(column => (byte)((((row + 1) * (column + seed)) * 37) + (row * 11))).ToArray())
            .ToArray();

    [Theory]
    [MemberData(nameof(Layouts))]
    public void ReversesEveryFilterAtEveryPixelSize(int colorType, int bitDepth, int width)
    {
        PngHeader header = new PngHeader(width, 6, bitDepth, (PngColorType)colorType, false);
        int rowBytes = (int)header.RowBytes(width);
        byte[][] rows = Pattern(6, rowBytes, colorType + bitDepth);
        byte[] palette = colorType == 3 ? TestPng.Chunk("PLTE", new byte[48]) : [];

        for (byte filter = 0; filter <= 4; filter++)
        {
            byte[] filtered = TestPng.Filter(rows, header.FilterStep, filter);
            byte[][] chunks = colorType == 3 ? [palette] : [];

            Assert.Equal(rows, Decode(TestPng.Image(width, 6, bitDepth, colorType, filtered, false, chunks)));
        }
    }

    [Fact]
    public void ReversesADifferentFilterOnEveryRow()
    {
        byte[][] rows = Pattern(10, 12, 3);
        byte[] filters = [4, 3, 2, 1, 0, 4, 4, 3, 3, 1];

        byte[] png = TestPng.Image(4, 10, 8, 2, TestPng.Filter(rows, 3, filters));

        Assert.Equal(rows, Decode(png));
    }

    [Fact]
    public void ReversesTheAverageFilterWithoutOverflowing()
    {
        // Left and above both 255: their sum does not fit a byte, and the average must still be 255.
        byte[][] rows = [[255, 255, 255, 255], [255, 255, 255, 255]];

        Assert.Equal(rows, Decode(TestPng.Image(4, 2, 8, 0, TestPng.Filter(rows, 1, 3))));
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(10, 0, 250)]
    [InlineData(200, 100, 150)]
    [InlineData(0, 0, 0)]
    [InlineData(255, 0, 128)]
    [InlineData(5, 10, 5)]
    [InlineData(10, 5, 5)]
    [InlineData(7, 7, 20)]
    public void PredictsLikeThePaethDefinition(int left, int above, int upperLeft)
    {
        Assert.Equal(
            (byte)TestPng.Paeth(left, above, upperLeft),
            PngFilters.Predict((byte)left, (byte)above, (byte)upperLeft));
    }

    [Fact]
    public void PredictsEveryCombinationLikeThePaethDefinition()
    {
        for (int left = 0; left < 256; left += 15)
        {
            for (int above = 0; above < 256; above += 15)
            {
                for (int upperLeft = 0; upperLeft < 256; upperLeft += 15)
                    Assert.Equal((byte)TestPng.Paeth(left, above, upperLeft), PngFilters.Predict((byte)left, (byte)above, (byte)upperLeft));
            }
        }
    }

    [Fact]
    public void AppliesPaethAsTheInverseOfItsReversal()
    {
        byte[][] rows = Pattern(3, 12, 5);
        byte[] previous = new byte[12];
        byte[] output = new byte[13];

        foreach (byte[] row in rows)
        {
            PngFilters.ApplyPaeth(row, previous, output, 3);
            Assert.Equal(PngFilters.Paeth, output[0]);

            byte[] restored = output.AsSpan(1).ToArray();
            PngFilters.Unfilter(output[0], restored, previous, 3);
            Assert.Equal(row, restored);
            previous = row;
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(255)]
    public void RejectsAnUndefinedFilterType(byte filter)
    {
        byte[][] rows = Pattern(2, 4, 1);
        byte[] filtered = TestPng.Filter(rows, 1, 0);
        filtered[5] = filter;

        Assert.Contains($"row filter type {filter}", Fails(TestPng.Image(4, 2, 8, 0, filtered)));
    }

    [Fact]
    public void RejectsImageDataThatEndsEarly()
    {
        byte[] filtered = TestPng.Filter(Pattern(3, 4, 1), 1, 0);

        byte[] png = TestPng.Image(4, 4, 8, 0, filtered);

        Assert.Contains("ends before all of the image has been read", Fails(png));
    }

    [Fact]
    public void RejectsInterlacedImageDataThatEndsEarly()
    {
        byte[] png = TestPng.Image(8, 8, 8, 0, new byte[20], interlaced: true);

        Assert.Contains("ends before all of the image has been read", Fails(png));
    }

    [Fact]
    public void ToleratesExtraImageDataWithAValidChecksum()
    {
        byte[][] rows = Pattern(2, 4, 1);
        byte[] filtered = [.. TestPng.Filter(rows, 1, 0), 0, 9, 9, 9, 9];

        Assert.Equal(rows, Decode(TestPng.Image(4, 2, 8, 0, filtered)));
    }

    [Theory]
    [InlineData(1, 65536, true)]
    [InlineData(1, 65537, false)]
    [InlineData(300, 300 * 301, true)]
    [InlineData(300, (300 * 301) + 1, false)]
    public void ReadsTrailingImageDataOnlyAsFarAsTheImageOr64KiB(int size, int trailing, bool accepted)
    {
        byte[] filtered = [.. new byte[size * (size + 1)], .. new byte[trailing]];
        byte[] png = TestPng.Image(size, size, 8, 0, filtered);

        if (accepted)
            Assert.Equal(size, Decode(png).Length);
        else
            Assert.Contains("continues far past the end of the image", Fails(png));
    }

    [Fact]
    public void RejectsImageDataFailingItsChecksum()
    {
        byte[] zlib = TestZlib.Compress(TestPng.Filter(Pattern(2, 4, 1), 1, 0));
        zlib[zlib.Length - 1] ^= 1;

        byte[] png = TestPng.Build(TestPng.Header(4, 2, 8, 0), TestPng.Data(zlib), TestPng.End());

        Assert.Contains("Adler-32", Fails(png));
    }

    [Fact]
    public void DecodesImageDataSplitAcrossChunksAtAnyByte()
    {
        byte[][] rows = Pattern(5, 6, 2);
        byte[] zlib = TestZlib.Compress(TestPng.Filter(rows, 2, 4));
        List<byte[]> chunks = [TestPng.Header(3, 5, 16, 0)];
        for (int offset = 0; offset < zlib.Length; offset += 3)
            chunks.Add(TestPng.Data(zlib.AsSpan(offset, Math.Min(3, zlib.Length - offset)).ToArray()));
        chunks.Add(TestPng.End());

        Assert.Equal(rows, Decode(TestPng.Build(chunks.ToArray())));
    }

    [Fact]
    public void RefusesAHeaderPromisingFarMoreDataThanThereIs()
    {
        // 16384 × 16384 gray pixels from a few bytes: more than deflate can expand to, rejected before inflating.
        byte[] png = TestPng.Image(16384, 16384, 8, 0, new byte[16]);

        string message = Fails(png);

        Assert.Contains("cannot hold", message);
        Assert.Contains("268451840 bytes", message);
    }

    [Theory]
    [InlineData(99, "valid zlib header")]
    [InlineData(98, "cannot hold")]
    public void RefusesImageDataBeyondDeflatesMaximumExpansionExactly(int dataLength, string failure)
    {
        // 100 rows of 1031 pixels need 100 × 1032 bytes inflated: exactly the most 99 compressed bytes can hold,
        // and more than 98 can. The data is not a zlib stream, so passing the size check fails on the header.
        byte[] data = Enumerable.Repeat((byte)0x11, dataLength).ToArray();
        byte[] png = TestPng.Build(TestPng.Header(1031, 100, 8, 0), TestPng.Data(data), TestPng.End());

        Assert.Contains(failure, Fails(png));
    }

    [Fact]
    public void AcceptsAHeaderWithinDeflatesMaximumExpansion()
    {
        // 1032 bytes of output per byte of input is the most deflate can produce; this image stays within it.
        byte[] raw = new byte[(1 + 2000) * 2000];
        byte[] png = TestPng.Image(2000, 2000, 8, 0, raw);

        Assert.Equal(2000, Decode(png).Length);
    }

    [Fact]
    public void RefusesRowsLongerThanAnArrayCanHold()
    {
        byte[] png = TestPng.Image(1 << 28, 1, 16, 6, new byte[16]);

        Assert.Contains("rows, 2147483648 bytes each, are too long", Fails(png));
    }

    [Fact]
    public void RefusesAnInterlacedImageTooLargeToHoldWhole()
    {
        byte[] png = TestPng.Image(16384, 16384, 16, 6, new byte[16], interlaced: true);

        Assert.Contains("needs 2147483648 bytes to de-interlace", Fails(png));
    }

    [Theory]
    [InlineData(1, 1, false, 2)]
    [InlineData(1, 1, true, 2)]
    [InlineData(3, 2, false, 8)]
    [InlineData(8, 8, true, 2 + 2 + 3 + 6 + 10 + 20 + 36)]
    [InlineData(2, 1, true, 4)]
    [InlineData(1, 2, true, 4)]
    [InlineData(5, 5, true, 36)]
    public void ComputesTheInflatedLengthOfEveryPass(int width, int height, bool interlaced, long expected)
    {
        Assert.Equal(expected, PngScanlineDecoder.ExpectedLength(new PngHeader(width, height, 8, PngColorType.Gray, interlaced)));
    }

    [Fact]
    public void ComputesTheInflatedLengthOfPackedPixels()
    {
        // 9 pixels at 1 bit need 2 bytes, plus the filter-type byte, on each of 3 rows.
        Assert.Equal(3 * 3, PngScanlineDecoder.ExpectedLength(new PngHeader(9, 3, 1, PngColorType.Gray, false)));
    }

    [Theory]
    [InlineData(0, 0, 0, 8, 8)]
    [InlineData(1, 4, 0, 8, 8)]
    [InlineData(2, 0, 4, 4, 8)]
    [InlineData(3, 2, 0, 4, 4)]
    [InlineData(4, 0, 2, 2, 4)]
    [InlineData(5, 1, 0, 2, 2)]
    [InlineData(6, 0, 1, 1, 2)]
    public void DescribesTheAdam7Passes(int pass, int columnStart, int rowStart, int columnStep, int rowStep)
    {
        Assert.Equal(columnStart, Adam7.ColumnStart(pass));
        Assert.Equal(rowStart, Adam7.RowStart(pass));
        Assert.Equal(columnStep, Adam7.ColumnStep(pass));
        Assert.Equal(rowStep, Adam7.RowStep(pass));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(0, 8, 1)]
    [InlineData(0, 9, 2)]
    [InlineData(1, 4, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(1, 13, 2)]
    [InlineData(5, 1, 0)]
    [InlineData(5, 2, 1)]
    [InlineData(6, 7, 7)]
    public void CountsTheColumnsOfEachPass(int pass, int width, int columns)
    {
        Assert.Equal(columns, Adam7.Columns(pass, width));
    }

    [Theory]
    [InlineData(2, 4, 0)]
    [InlineData(2, 5, 1)]
    [InlineData(6, 1, 0)]
    [InlineData(6, 2, 1)]
    [InlineData(6, 4, 2)]
    [InlineData(0, int.MaxValue, 268435456)]
    public void CountsTheRowsOfEachPass(int pass, int height, int rows)
    {
        Assert.Equal(rows, Adam7.Rows(pass, height));
    }

    // Rents buffers of every size a small decode uses, fills them with ones and returns them, so that the next rents
    // on this thread are handed stale memory.
    private static void DirtyThePool()
    {
        for (int size = 16; size <= 1 << 16; size *= 2)
        {
            byte[][] rented = [ArrayPool<byte>.Shared.Rent(size), ArrayPool<byte>.Shared.Rent(size), ArrayPool<byte>.Shared.Rent(size)];
            foreach (byte[] buffer in rented)
            {
                buffer.AsSpan().Fill(0xFF);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    [Theory]
    [InlineData("s07i3p02.png", "s07n3p02.png")]
    [InlineData("basi0g01.png", "basn0g01.png")]
    [InlineData("basi2c16.png", "basn2c16.png")]
    public void DecodesCorrectlyFromStaleBuffers(string interlaced, string sequential)
    {
        byte[][] expected = Decode(TestImageFiles.Bytes(sequential));

        DirtyThePool();
        byte[][] fromStale = Decode(TestImageFiles.Bytes(interlaced));
        DirtyThePool();
        byte[][] sequentialFromStale = Decode(TestImageFiles.Bytes(sequential));

        Assert.Equal(expected, fromStale);
        Assert.Equal(expected, sequentialFromStale);
    }

    [Fact]
    public void LeavesThePaddingBitsOfDeinterlacedRowsClear()
    {
        DirtyThePool();

        byte[][] rows = Decode(TestImageFiles.Bytes("s07i3p02.png"));

        // Seven 2-bit pixels fill 14 of 16 bits; the last two are padding.
        Assert.All(rows, row => Assert.Equal(0, row[1] & 0x03));
    }
}
