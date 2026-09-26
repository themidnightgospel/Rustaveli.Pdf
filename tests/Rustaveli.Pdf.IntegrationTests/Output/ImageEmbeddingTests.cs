using System.Text;
using System.Text.RegularExpressions;
using Rustaveli.Pdf.Images;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// Every image an export embeds, read back by an independent parser: the XObject must say exactly what the encoder
/// produced — size, colour space, filter and its parameters, masks — or a reader decodes different pixels.
/// </summary>
public class ImageEmbeddingTests
{
    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    public static TheoryData<string> Fixtures() => new TheoryData<string>(FixtureNames());

    private static IEnumerable<string> FixtureNames() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "assets", "images"))
            .Select(Path.GetFileName)
            .Select(name => name!)

            // PngSuite names its corrupt files with a leading x.
            .Where(name => (name.EndsWith(".png", StringComparison.Ordinal) && !name.StartsWith('x')) || name.EndsWith(".jpg", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

    private static byte[] Export(params RasterImage[] images) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 300 * Math.Max(1, images.Length));
            section.Body().Stack(stack =>
            {
                foreach (RasterImage image in images)
                    stack.Add().Height(100).Image(image, ImageFitting.Proportionally);
            });
        })).ExportPdf(new PdfExportOptions { Compress = false });

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TheImageDictionaryDescribesWhatWasEncoded(string name)
    {
        RasterImage image = RasterImage.FromFile(PathOf(name));
        EncodedImage encoded = image.Encode();

        using PdfDocument pdf = PdfDocument.Open(Export(image));
        DictionaryToken dictionary = Assert.Single(pdf.GetPage(1).GetImages()).ImageDictionary;

        Assert.Equal(encoded.Width, Number(dictionary, "Width"));
        Assert.Equal(encoded.Height, Number(dictionary, "Height"));
        Assert.Equal(encoded.BitsPerComponent, Number(dictionary, "BitsPerComponent"));
        Assert.Equal(encoded.Filter == ImageFilter.Dct ? "DCTDecode" : "FlateDecode", Name(dictionary, "Filter"));
        Assert.Equal(SpaceName(encoded.ColorSpace), ColorSpaceName(dictionary));
        Assert.Equal(encoded.SoftMask is not null, dictionary.ContainsKey(NameToken.Create("SMask")));
        Assert.Equal(encoded.ColorKeyMask is not null, dictionary.ContainsKey(NameToken.Create("Mask")));
        Assert.Equal(encoded.Decode is not null, dictionary.ContainsKey(NameToken.Create("Decode")));
        Assert.Equal(
            encoded.DecodeParameters is not null || encoded.ColorTransform is not null,
            dictionary.ContainsKey(NameToken.Create("DecodeParms")));
    }

    [Fact]
    public void ImagesSharingAnIccProfileShareOneProfileStream()
    {
        // The same profile ahead of different pixels: two images, one profile.
        byte[] original = File.ReadAllBytes(PathOf("jpeg-icc.jpg"));
        byte[] altered = (byte[])original.Clone();
        altered[altered.Length - 3] ^= 0x01;

        string pdf = Encoding.Latin1.GetString(Export(RasterImage.FromBytes(original), RasterImage.FromBytes(altered)));

        Assert.Equal(2, Regex.Matches(pdf, @"/Subtype\s*/Image\b").Count);
        Assert.Single(Regex.Matches(pdf, @"/Alternate\s*/Device"));
    }

    [Fact]
    public void AnEmbeddedJpegIsTheFileItself()
    {
        byte[] jpeg = File.ReadAllBytes(PathOf("jpeg-baseline.jpg"));

        using PdfDocument pdf = PdfDocument.Open(Export(RasterImage.FromBytes(jpeg)));

        Assert.Equal(jpeg, Assert.Single(pdf.GetPage(1).GetImages()).RawBytes.ToArray());
    }

    [Fact]
    public void AnImageTurnedByItsOrientationIsLaidOutUpright()
    {
        // Stored 48 wide and 32 tall with orientation 6: upright it is 32 wide and 48 tall, so fitting it
        // proportionally into a box 100 tall makes it narrower than tall. The conformance suite renders it.
        RasterImage image = RasterImage.FromFile(PathOf("jpeg-exif-orientation6.jpg"));

        Assert.Equal((image.StoredHeight, image.StoredWidth), (image.PixelWidth, image.PixelHeight));
        Assert.True(image.PixelHeight > image.PixelWidth);
    }

    private static string SpaceName(ImageColorSpace space) => space.Kind switch
    {
        ImageColorSpaceKind.DeviceGray => "DeviceGray",
        ImageColorSpaceKind.DeviceRgb => "DeviceRGB",
        ImageColorSpaceKind.DeviceCmyk => "DeviceCMYK",
        ImageColorSpaceKind.Indexed => "Indexed",
        _ => "ICCBased",
    };

    private static string ColorSpaceName(DictionaryToken dictionary) => dictionary.Data["ColorSpace"] switch
    {
        NameToken name => name.Data,
        ArrayToken array => ((NameToken)array.Data[0]).Data,
        IToken other => other.ToString()!,
    };

    private static double Number(DictionaryToken dictionary, string key) => ((NumericToken)dictionary.Data[key]).Data;

    private static string Name(DictionaryToken dictionary, string key) => ((NameToken)dictionary.Data[key]).Data;
}
