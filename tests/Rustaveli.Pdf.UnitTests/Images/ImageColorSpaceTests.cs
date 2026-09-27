using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class ImageColorSpaceTests
{
    [Fact]
    public void DeviceSpacesDescribeThemselves()
    {
        Assert.Equal(ImageColorSpaceKind.DeviceGray, ImageColorSpace.DeviceGray.Kind);
        Assert.Equal(1, ImageColorSpace.DeviceGray.ComponentCount);
        Assert.Equal(ImageColorSpaceKind.DeviceRgb, ImageColorSpace.DeviceRgb.Kind);
        Assert.Equal(3, ImageColorSpace.DeviceRgb.ComponentCount);
        Assert.Equal(ImageColorSpaceKind.DeviceCmyk, ImageColorSpace.DeviceCmyk.Kind);
        Assert.Equal(4, ImageColorSpace.DeviceCmyk.ComponentCount);

        foreach (ImageColorSpace space in new[] { ImageColorSpace.DeviceGray, ImageColorSpace.DeviceRgb, ImageColorSpace.DeviceCmyk })
        {
            Assert.Null(space.Base);
            Assert.Null(space.Profile);
            Assert.Null(space.Alternate);
            Assert.True(space.Palette.IsEmpty);
            Assert.Equal(0, space.HighValue);
        }
    }

    [Theory]
    [InlineData(1, (int)ImageColorSpaceKind.DeviceGray)]
    [InlineData(3, (int)ImageColorSpaceKind.DeviceRgb)]
    [InlineData(4, (int)ImageColorSpaceKind.DeviceCmyk)]
    public void ForChoosesTheDeviceSpaceWithoutAProfile(int components, int kind)
    {
        Assert.Equal((ImageColorSpaceKind)kind, ImageColorSpace.For(components, null).Kind);
    }

    [Theory]
    [InlineData("GRAY", 1, (int)ImageColorSpaceKind.DeviceGray)]
    [InlineData("RGB ", 3, (int)ImageColorSpaceKind.DeviceRgb)]
    [InlineData("CMYK", 4, (int)ImageColorSpaceKind.DeviceCmyk)]
    public void ForChoosesAnIccBasedSpaceWithAProfile(string colorSpace, int components, int alternate)
    {
        IccProfile profile = IccProfile.TryCreate(TestJpeg.Profile(colorSpace), components)!;

        ImageColorSpace space = ImageColorSpace.For(components, profile);

        Assert.Equal(ImageColorSpaceKind.IccBased, space.Kind);
        Assert.Same(profile, space.Profile);
        Assert.Equal(components, space.ComponentCount);
        Assert.Equal((ImageColorSpaceKind)alternate, space.Alternate!.Kind);
        Assert.Null(space.Base);
    }

    [Fact]
    public void IndexedSpacesCarryTheirPaletteAndHighestIndex()
    {
        byte[] palette = [255, 0, 0, 0, 255, 0, 0, 0, 255, 9, 9, 9];

        ImageColorSpace space = ImageColorSpace.Indexed(ImageColorSpace.DeviceRgb, palette);

        Assert.Equal(ImageColorSpaceKind.Indexed, space.Kind);
        Assert.Equal(1, space.ComponentCount);
        Assert.Same(ImageColorSpace.DeviceRgb, space.Base);
        Assert.Equal(palette, space.Palette.ToArray());
        Assert.Equal(3, space.HighValue);
        Assert.Null(space.Profile);
        Assert.Null(space.Alternate);
    }

    [Fact]
    public void AnIndexedSpaceCountsEntriesInItsBaseSpace()
    {
        ImageColorSpace gray = ImageColorSpace.Indexed(ImageColorSpace.DeviceGray, new byte[256]);
        ImageColorSpace cmyk = ImageColorSpace.Indexed(ImageColorSpace.DeviceCmyk, new byte[8]);

        Assert.Equal(255, gray.HighValue);
        Assert.Equal(1, cmyk.HighValue);
    }

    [Fact]
    public void FlateParametersUsePngPrediction()
    {
        FlateDecodeParameters parameters = new FlateDecodeParameters(3, 16, 640);

        Assert.Equal(15, parameters.Predictor);
        Assert.Equal(3, parameters.Colors);
        Assert.Equal(16, parameters.BitsPerComponent);
        Assert.Equal(640, parameters.Columns);
    }

    [Fact]
    public void EncodedImagesKeepWhatTheyWereGiven()
    {
        EncodedImage mask = new EncodedImage(2, 1, ImageColorSpace.DeviceGray, 8, ImageFilter.Flate, new byte[] { 1 });
        FlateDecodeParameters parameters = new FlateDecodeParameters(1, 8, 2);

        EncodedImage image = new EncodedImage(
            2, 1, ImageColorSpace.DeviceRgb, 8, ImageFilter.Flate, new byte[] { 7, 8 }, parameters, 0, [1d, 0d], [3, 3], mask);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Same(ImageColorSpace.DeviceRgb, image.ColorSpace);
        Assert.Equal(8, image.BitsPerComponent);
        Assert.Equal(ImageFilter.Flate, image.Filter);
        Assert.Equal(new byte[] { 7, 8 }, image.Data.ToArray());
        Assert.Same(parameters, image.DecodeParameters);
        Assert.Equal(0, image.ColorTransform);
        Assert.Equal(new[] { 1d, 0d }, image.Decode);
        Assert.Equal(new[] { 3, 3 }, image.ColorKeyMask);
        Assert.Same(mask, image.SoftMask);

        Assert.Null(mask.DecodeParameters);
        Assert.Null(mask.ColorTransform);
        Assert.Null(mask.Decode);
        Assert.Null(mask.ColorKeyMask);
        Assert.Null(mask.SoftMask);
    }

    [Fact]
    public void MetadataDefaultsToNothing()
    {
        ImageMetadata metadata = new ImageMetadata();

        Assert.Equal(ExifOrientation.Normal, metadata.Orientation);
        Assert.Null(metadata.IccProfile);
        Assert.Null(metadata.Gamma);
        Assert.Null(metadata.Chromaticities);
        Assert.Null(metadata.RenderingIntent);
    }
}
