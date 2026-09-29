using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// JPEGs are embedded exactly as they are; what matters is that the dictionary describing them is right. The
/// generated fixtures are 48 × 32, so a swapped width and height cannot pass.
/// </summary>
public class JpegTests
{
    private static readonly byte[] Rgb = [(byte)'R', (byte)'G', (byte)'B'];

    private static readonly double[] InvertedCmyk = [1, 0, 1, 0, 1, 0, 1, 0];

    private static JpegFile Parse(byte[] data) => JpegParser.Parse(data);

    private static EncodedImage Encode(byte[] data) => JpegParser.Parse(data).Encode();

    [Theory]
    [InlineData("jpeg-baseline.jpg", 3, false)]
    [InlineData("jpeg-progressive.jpg", 3, true)]
    [InlineData("jpeg-gray.jpg", 1, false)]
    [InlineData("jpeg-cmyk-adobe.jpg", 4, false)]
    [InlineData("jpeg-rgb-adobe.jpg", 3, false)]
    [InlineData("jpeg-exif-orientation6.jpg", 3, false)]
    [InlineData("jpeg-icc.jpg", 3, false)]
    public void ReadsTheFrameOfEveryFixture(string name, int components, bool progressive)
    {
        JpegFile jpeg = Parse(TestImageFiles.Bytes(name));

        Assert.Equal(48, jpeg.Width);
        Assert.Equal(32, jpeg.Height);
        Assert.Equal(components, jpeg.ComponentCount);
        Assert.Equal(progressive, jpeg.IsProgressive);
    }

    [Theory]
    [InlineData("jpeg-baseline.jpg")]
    [InlineData("jpeg-progressive.jpg")]
    [InlineData("jpeg-gray.jpg")]
    [InlineData("jpeg-cmyk-adobe.jpg")]
    [InlineData("jpeg-icc.jpg")]
    public void EmbedsTheFileByteForByteAsDctData(string name)
    {
        byte[] file = TestImageFiles.Bytes(name);

        EncodedImage image = Encode(file);

        Assert.Equal(ImageFilter.Dct, image.Filter);
        Assert.Equal(file, image.Data.ToArray());
        Assert.Equal(48, image.Width);
        Assert.Equal(32, image.Height);
        Assert.Equal(8, image.BitsPerComponent);
        Assert.Null(image.DecodeParameters);
        Assert.Null(image.SoftMask);
        Assert.Null(image.ColorKeyMask);
    }

    [Fact]
    public void DescribesAYCbCrJpegAsDeviceRgbWithReaderDefaults()
    {
        EncodedImage image = Encode(TestImageFiles.Bytes("jpeg-baseline.jpg"));

        Assert.Same(ImageColorSpace.DeviceRgb, image.ColorSpace);
        Assert.Null(image.ColorTransform);
        Assert.Null(image.Decode);
    }

    [Fact]
    public void DescribesAGrayJpegAsDeviceGray()
    {
        EncodedImage image = Encode(TestImageFiles.Bytes("jpeg-gray.jpg"));

        Assert.Same(ImageColorSpace.DeviceGray, image.ColorSpace);
        Assert.Null(image.Decode);
    }

    [Fact]
    public void InvertsAdobeCmykWithADecodeArray()
    {
        JpegFile jpeg = Parse(TestImageFiles.Bytes("jpeg-cmyk-adobe.jpg"));
        EncodedImage image = jpeg.Encode();

        Assert.True(jpeg.HasAdobeMarker);
        Assert.Same(ImageColorSpace.DeviceCmyk, image.ColorSpace);
        Assert.Equal(InvertedCmyk, image.Decode);
        Assert.Null(image.ColorTransform);
    }

    [Fact]
    public void LeavesCmykWithoutAnAdobeSegmentUninverted()
    {
        EncodedImage image = Encode(TestJpeg.Build(TestJpeg.Frame(10, 20, 4)));

        Assert.Same(ImageColorSpace.DeviceCmyk, image.ColorSpace);
        Assert.Null(image.Decode);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    public void InvertsCmykAndYcckWhateverTheAdobeTransform(byte transform)
    {
        EncodedImage image = Encode(TestJpeg.Build(TestJpeg.Adobe(transform), TestJpeg.Frame(10, 20, 4)));

        Assert.Equal(InvertedCmyk, image.Decode);
    }

    [Fact]
    public void DoesNotInvertThreeComponentsBehindAnAdobeSegment()
    {
        EncodedImage image = Encode(TestJpeg.Build(TestJpeg.Adobe(1), TestJpeg.Frame(10, 20, 3)));

        Assert.Null(image.Decode);
    }

    [Fact]
    public void LeavesTheRgbFixtureToItsAdobeSegment()
    {
        JpegFile jpeg = Parse(TestImageFiles.Bytes("jpeg-rgb-adobe.jpg"));

        Assert.True(jpeg.HasAdobeMarker);
        Assert.False(jpeg.IsRgb);
        Assert.Null(jpeg.Encode().ColorTransform);
    }

    [Fact]
    public void TellsTheReaderNotToTransformRgbComponents()
    {
        JpegFile jpeg = Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, Rgb)));

        Assert.True(jpeg.IsRgb);
        Assert.Equal(0, jpeg.Encode().ColorTransform);
        Assert.Same(ImageColorSpace.DeviceRgb, jpeg.Encode().ColorSpace);
    }

    [Fact]
    public void TreatsRgbIdentifiersUnderJfifAsYCbCr()
    {
        JpegFile jpeg = Parse(TestJpeg.Build(TestJpeg.Jfif(), TestJpeg.Frame(10, 20, Rgb)));

        Assert.False(jpeg.IsRgb);
        Assert.Null(jpeg.Encode().ColorTransform);
    }

    [Fact]
    public void TreatsRgbIdentifiersUnderAnAdobeSegmentAsAdobeSays()
    {
        Assert.False(Parse(TestJpeg.Build(TestJpeg.Adobe(0), TestJpeg.Frame(10, 20, Rgb))).IsRgb);
    }

    [Theory]
    [InlineData((byte)'r', (byte)'G', (byte)'B')]
    [InlineData((byte)'R', (byte)'g', (byte)'B')]
    [InlineData((byte)'R', (byte)'G', (byte)'b')]
    [InlineData((byte)1, (byte)2, (byte)3)]
    public void TreatsOtherIdentifiersAsYCbCr(byte first, byte second, byte third)
    {
        Assert.False(Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, [first, second, third]))).IsRgb);
    }

    [Fact]
    public void RemembersJfifAndAdobeSegmentsThatAppearTwice()
    {
        JpegFile jfif = Parse(TestJpeg.Build(TestJpeg.Jfif(), TestJpeg.Jfif(), TestJpeg.Frame(10, 20, Rgb)));
        JpegFile adobe = Parse(TestJpeg.Build(TestJpeg.Adobe(0), TestJpeg.Adobe(0), TestJpeg.Frame(10, 20, 4)));

        Assert.False(jfif.IsRgb);
        Assert.True(adobe.HasAdobeMarker);
        Assert.NotNull(adobe.Encode().Decode);
    }

    [Fact]
    public void RecognisesJfifOnlyByItsFullIdentifier()
    {
        byte[] notJfif = TestJpeg.Segment(0xE0, [.. "JFXX\0"u8, 0x10]);

        Assert.True(Parse(TestJpeg.Build(notJfif, TestJpeg.Frame(10, 20, Rgb))).IsRgb);
    }

    [Fact]
    public void RecognisesAdobeOnlyWithAFullSegment()
    {
        byte[] shortAdobe = TestJpeg.Segment(0xEE, [.. "Adobe"u8, 0, 100, 0, 0, 0, 0]);
        byte[] otherApp14 = TestJpeg.Segment(0xEE, [.. "Adobf"u8, 0, 100, 0, 0, 0, 0, 1]);

        Assert.False(Parse(TestJpeg.Build(shortAdobe, TestJpeg.Frame(10, 20, 4))).HasAdobeMarker);
        Assert.False(Parse(TestJpeg.Build(otherApp14, TestJpeg.Frame(10, 20, 4))).HasAdobeMarker);
        Assert.True(Parse(TestJpeg.Build(TestJpeg.Adobe(0), TestJpeg.Frame(10, 20, 4))).HasAdobeMarker);
    }

    [Fact]
    public void ReportsTheExifOrientationOfTheFixture()
    {
        Assert.Equal(ExifOrientation.Rotate90, Parse(TestImageFiles.Bytes("jpeg-exif-orientation6.jpg")).Orientation);
        Assert.Equal(ExifOrientation.Normal, Parse(TestImageFiles.Bytes("jpeg-baseline.jpg")).Orientation);
    }

    [Fact]
    public void ReadsOrientationFromTheFirstExifSegmentOnly()
    {
        JpegFile jpeg = Parse(TestJpeg.Build(
            TestJpeg.Exif(TestJpeg.Orientation(8, bigEndian: true)),
            TestJpeg.Exif(TestJpeg.Orientation(3)),
            TestJpeg.Frame(10, 20, 3)));

        Assert.Equal(ExifOrientation.Rotate270, jpeg.Orientation);
    }

    [Fact]
    public void IgnoresApp1SegmentsThatAreNotExif()
    {
        byte[] xmp = TestJpeg.Segment(0xE1, [.. "http://ns.adobe.com/xap/1.0/\0"u8, 1, 2, 3]);

        JpegFile jpeg = Parse(TestJpeg.Build(xmp, TestJpeg.Exif(TestJpeg.Orientation(5)), TestJpeg.Frame(10, 20, 3)));

        Assert.Equal(ExifOrientation.Transpose, jpeg.Orientation);
    }

    [Fact]
    public void ReportsAnEmptyExifSegmentAsNormal()
    {
        JpegFile jpeg = Parse(TestJpeg.Build(TestJpeg.Exif([]), TestJpeg.Exif(TestJpeg.Orientation(6)), TestJpeg.Frame(10, 20, 3)));

        Assert.Equal(ExifOrientation.Normal, jpeg.Orientation);
    }

    [Fact]
    public void ExtractsTheIccProfileOfTheFixture()
    {
        JpegFile jpeg = Parse(TestImageFiles.Bytes("jpeg-icc.jpg"));
        EncodedImage image = jpeg.Encode();

        Assert.NotNull(jpeg.IccProfile);
        Assert.Equal(588, jpeg.IccProfile.Data.Length);
        Assert.Equal(3, jpeg.IccProfile.ComponentCount);
        Assert.Equal(ImageColorSpaceKind.IccBased, image.ColorSpace.Kind);
        Assert.Same(jpeg.IccProfile, image.ColorSpace.Profile);
        Assert.Same(ImageColorSpace.DeviceRgb, image.ColorSpace.Alternate);
        Assert.Equal(3, image.ColorSpace.ComponentCount);
    }

    [Fact]
    public void ReassemblesAProfileSplitAcrossSegmentsInAnyOrder()
    {
        byte[] profile = TestJpeg.Profile("CMYK", size: 300);

        JpegFile jpeg = Parse(TestJpeg.Build(
            TestJpeg.Icc(3, 3, profile.AsSpan(200).ToArray()),
            TestJpeg.Icc(1, 3, profile.AsSpan(0, 100).ToArray()),
            TestJpeg.Icc(2, 3, profile.AsSpan(100, 100).ToArray()),
            TestJpeg.Frame(10, 20, 4)));

        Assert.NotNull(jpeg.IccProfile);
        Assert.Equal(profile, jpeg.IccProfile.Data.ToArray());
        Assert.Equal(4, jpeg.IccProfile.ComponentCount);
    }

    [Fact]
    public void KeepsASingleSegmentProfileInPlace()
    {
        byte[] profile = TestJpeg.Profile("GRAY");
        byte[] file = TestJpeg.Build(TestJpeg.Icc(1, 1, profile), TestJpeg.Frame(10, 20, 1));

        JpegFile jpeg = Parse(file);

        Assert.NotNull(jpeg.IccProfile);
        Assert.Equal(profile, jpeg.IccProfile.Data.ToArray());
    }

    public static TheoryData<int[]> IncompleteSequences => new TheoryData<int[]>
    {
        new[] { 1, 3, 2, 3 },
        new[] { 1, 2, 1, 2 },
        new[] { 1, 2, 1, 2, 2, 2 },
        new[] { 0, 2, 1, 2 },
        new[] { 1, 2, 3, 2 },
        new[] { 1, 2, 2, 3 },
        new[] { 1, 0 },
        new[] { 2, 2 },
    };

    [Theory]
    [MemberData(nameof(IncompleteSequences))]
    public void DropsAProfileWithMissingDuplicateOrInconsistentSegments(int[] sequenceAndCount)
    {
        byte[] profile = TestJpeg.Profile("RGB ", size: 300);
        List<byte[]> segments = new List<byte[]>();
        int pieces = sequenceAndCount.Length / 2;
        int size = profile.Length / pieces;
        for (int index = 0; index < pieces; index++)
        {
            int end = index == pieces - 1 ? profile.Length : (index + 1) * size;
            byte[] piece = profile.AsSpan(index * size, end - (index * size)).ToArray();
            segments.Add(TestJpeg.Icc(sequenceAndCount[index * 2], sequenceAndCount[(index * 2) + 1], piece));
        }

        segments.Add(TestJpeg.Frame(10, 20, 3));

        Assert.Null(Parse(TestJpeg.Build(segments.ToArray())).IccProfile);
    }

    [Fact]
    public void DropsAProfileForTheWrongNumberOfComponents()
    {
        JpegFile jpeg = Parse(TestJpeg.Build(TestJpeg.Icc(1, 1, TestJpeg.Profile("RGB ")), TestJpeg.Frame(10, 20, 1)));

        Assert.Null(jpeg.IccProfile);
        Assert.Same(ImageColorSpace.DeviceGray, jpeg.Encode().ColorSpace);
    }

    [Fact]
    public void IgnoresApp2SegmentsThatCarryNoProfileData()
    {
        byte[] empty = TestJpeg.Segment(0xE2, [.. "ICC_PROFILE\0"u8, 1, 1]);
        byte[] other = TestJpeg.Segment(0xE2, [.. "FPXR\0"u8, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

        JpegFile jpeg = Parse(TestJpeg.Build(empty, other, TestJpeg.Icc(1, 1, TestJpeg.Profile("RGB ")), TestJpeg.Frame(10, 20, 3)));

        Assert.NotNull(jpeg.IccProfile);
    }

    [Theory]
    [InlineData((byte)0xC0, false)]
    [InlineData((byte)0xC1, false)]
    [InlineData((byte)0xC2, true)]
    public void AcceptsBaselineExtendedAndProgressiveFrames(byte marker, bool progressive)
    {
        JpegFile jpeg = Parse(TestJpeg.Build(TestJpeg.Frame(300, 200, 3, marker)));

        Assert.Equal(300, jpeg.Width);
        Assert.Equal(200, jpeg.Height);
        Assert.Equal(progressive, jpeg.IsProgressive);
    }

    [Theory]
    [InlineData((byte)0xC3)]
    [InlineData((byte)0xC5)]
    [InlineData((byte)0xC6)]
    [InlineData((byte)0xC7)]
    [InlineData((byte)0xC9)]
    [InlineData((byte)0xCA)]
    [InlineData((byte)0xCB)]
    [InlineData((byte)0xCD)]
    [InlineData((byte)0xCE)]
    [InlineData((byte)0xCF)]
    public void RefusesProcessesPdfReadersNeedNotDecode(byte marker)
    {
        UnsupportedImageFormatException error = Assert.Throws<UnsupportedImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, 3, marker))));

        Assert.Equal(ImageFormat.Jpeg, error.Format);
        Assert.Contains($"SOF{marker - 0xC0}", error.Message);
        Assert.EndsWith(
            "which PDF readers are not required to decode; only baseline, extended and progressive Huffman-coded " +
            "JPEGs can be embedded.",
            error.Message);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(16)]
    public void RefusesSamplesOtherThanEightBits(int precision)
    {
        UnsupportedImageFormatException error = Assert.Throws<UnsupportedImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, 3, 0xC1, precision))));

        Assert.Contains($"{precision}-bit", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void RefusesComponentCountsPdfCannotDescribe(int components)
    {
        UnsupportedImageFormatException error = Assert.Throws<UnsupportedImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, components))));

        Assert.Contains($"{components} colour components", error.Message);
        Assert.EndsWith("only 1 (gray), 3 (colour) and 4 (CMYK) can be embedded.", error.Message);
    }

    [Fact]
    public void RefusesAHeightDeferredToADnlMarker()
    {
        UnsupportedImageFormatException error = Assert.Throws<UnsupportedImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 0, 3))));

        Assert.Contains("DNL", error.Message);
    }

    [Fact]
    public void RejectsAZeroWidthAsMalformed()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => Parse(TestJpeg.Build(TestJpeg.Frame(0, 20, 3))));

        Assert.IsNotType<UnsupportedImageFormatException>(error);
    }

    [Fact]
    public void RejectsMoreThanTheMaximumPixelCount()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => Parse(TestJpeg.Build(TestJpeg.Frame(65535, 65535, 3))));

        Assert.StartsWith("The JPEG image is 65535 × 65535 pixels", error.Message);
    }

    [Fact]
    public void AcceptsExactlyTheMaximumPixelCount()
    {
        Assert.Equal(16384, Parse(TestJpeg.Build(TestJpeg.Frame(16384, 16384, 3))).Width);
    }

    [Fact]
    public void RejectsATruncatedFrameHeader()
    {
        byte[] shortHeader = TestJpeg.Segment(0xC0, [8, 0, 20, 0, 10]);
        byte[] missingComponent = TestJpeg.Segment(0xC0, [8, 0, 20, 0, 10, 3, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11]);

        Assert.Contains("frame header is truncated", Assert.Throws<ImageFormatException>(() => Parse(TestJpeg.Build(shortHeader))).Message);
        Assert.Contains("frame header is truncated", Assert.Throws<ImageFormatException>(() => Parse(TestJpeg.Build(missingComponent))).Message);
    }

    [Fact]
    public void AcceptsAFrameHeaderWithExactlyItsComponents()
    {
        byte[] exact = TestJpeg.Segment(0xC0, [8, 0, 20, 0, 10, 1, 1, 0x11, 0]);

        Assert.Equal(1, Parse(TestJpeg.Build(exact)).ComponentCount);
    }

    [Fact]
    public void RejectsASecondFrame()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, 3), TestJpeg.Frame(10, 20, 3, 0xC2))));

        Assert.Contains("more than one frame", error.Message);
    }

    [Fact]
    public void RejectsImageDataBeforeTheFrame()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => Parse(TestJpeg.Build()));

        Assert.Contains("before its frame header", error.Message);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xD9, 0xFF, 0xE0 })]
    [InlineData(new byte[] { 0x00, 0xD8, 0xFF, 0xE0 })]
    public void RejectsDataWithoutAStartOfImage(byte[] data)
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => Parse(data));

        Assert.Contains("start-of-image", error.Message);
    }

    [Fact]
    public void RejectsASecondStartOfImage()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build([0xFF, 0xD8], TestJpeg.Frame(10, 20, 3))));

        Assert.Contains("second start-of-image marker at offset 2", error.Message);
    }

    [Fact]
    public void RejectsAnEndOfImageBeforeAnyScan()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build(TestJpeg.Frame(10, 20, 3), [0xFF, 0xD9])));

        Assert.Contains("ends before any image data", error.Message);
    }

    [Fact]
    public void SkipsFillBytesAndMarkersWithoutALength()
    {
        JpegFile jpeg = Parse(TestJpeg.Build([0xFF, 0xFF, 0xFF, 0xD0, 0xFF, 0x01, 0xFF, 0xD7], TestJpeg.Frame(10, 20, 3)));

        Assert.Equal(10, jpeg.Width);
    }

    [Fact]
    public void SkipsSegmentsItHasNoUseFor()
    {
        byte[] comment = TestJpeg.Segment(0xFE, "made by hand"u8.ToArray());
        byte[] quantisation = TestJpeg.Segment(0xDB, new byte[65]);
        byte[] reserved = TestJpeg.Segment(0xC8, [1, 2, 3]);

        // Tables whose markers sit among the frame markers, and which are not frames.
        byte[] huffman = TestJpeg.Segment(0xC4, new byte[17]);
        byte[] conditioning = TestJpeg.Segment(0xCC, [0x00, 0x10]);

        JpegFile jpeg = Parse(TestJpeg.Build(comment, quantisation, reserved, huffman, conditioning, TestJpeg.Frame(10, 20, 3)));

        Assert.Equal(20, jpeg.Height);
    }

    [Fact]
    public void RejectsBytesWhereAMarkerShouldBe()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build([0x12], TestJpeg.Frame(10, 20, 3))));

        Assert.Contains("Expected a JPEG marker at offset 2", error.Message);
    }

    [Fact]
    public void RejectsAStuffedZeroOutsideTheScan()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build([0xFF, 0x00], TestJpeg.Frame(10, 20, 3))));

        Assert.Contains("stuffed zero byte", error.Message);
        Assert.Contains("offset 3", error.Message);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xFF, 0xFF })]
    public void RejectsDataThatEndsInFillBytes(byte[] data)
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(() => Parse(data));

        Assert.Contains("ends before any image data", error.Message);
    }

    [Fact]
    public void RejectsDataEndingInAMarkerCode()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Raw(TestJpeg.Frame(10, 20, 3), [0xFF, 0xFF])));

        Assert.Contains("ends before any image data", error.Message);
    }

    [Fact]
    public void RejectsDataEndingAfterASegment()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Raw(TestJpeg.Frame(10, 20, 3))));

        Assert.Contains("ends before any image data", error.Message);
    }

    [Fact]
    public void RejectsDataEndingInsideASegmentLength()
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Raw(TestJpeg.Frame(10, 20, 3), [0xFF, 0xE0, 0x00])));

        Assert.Contains("ends inside a marker segment", error.Message);
    }

    [Fact]
    public void RejectsASegmentRunningPastTheEnd()
    {
        byte[] segment = TestJpeg.Segment(0xE0, new byte[10]);

        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Raw(segment.AsSpan(0, segment.Length - 1).ToArray())));

        Assert.Contains("ends inside a marker segment", error.Message);
    }

    [Fact]
    public void AcceptsASegmentEndingExactlyWhereTheScanBegins()
    {
        byte[] file = TestJpeg.Raw(TestJpeg.Frame(10, 20, 3), [0xFF, 0xDA, 0x00, 0x02]);

        Assert.Equal(10, Parse(file).Width);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RejectsASegmentLengthBelowTwo(int length)
    {
        ImageFormatException error = Assert.Throws<ImageFormatException>(
            () => Parse(TestJpeg.Build([0xFF, 0xE0, 0x00, (byte)length], TestJpeg.Frame(10, 20, 3))));

        Assert.Contains($"invalid length of {length}", error.Message);
    }

    [Fact]
    public void AcceptsAnEmptySegment()
    {
        Assert.Equal(10, Parse(TestJpeg.Build([0xFF, 0xE3, 0x00, 0x02], TestJpeg.Frame(10, 20, 3))).Width);
    }
}
