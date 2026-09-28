using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Pages exported as images: one per page, at the resolution asked for, with everything where layout put it.
/// </summary>
public class ImageExportTests
{
    private static readonly Ink Red = Ink.Rgb(220, 20, 20);

    private static string ImagePath(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    /// <summary>Two pages, two inches by one: a red band on the first, a line of text on the second.</summary>
    private static Document TwoPages() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(144, 72);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(14);
        section.Body().Stack(stack =>
        {
            stack.Add().Height(36).Fill(Red);
            stack.Add().NewPage();
            stack.Add().Text("Second page");
        });
    }));

    private static SKBitmap Decode(byte[] image) => SKBitmap.Decode(image) ?? throw new InvalidOperationException("Not an image.");

    private static bool IsNear(SKColor actual, SKColor expected, int tolerance) =>
        Math.Abs(actual.Red - expected.Red) <= tolerance &&
        Math.Abs(actual.Green - expected.Green) <= tolerance &&
        Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    [Fact]
    public void ExportsOneImagePerPageAtTheResolutionAskedFor()
    {
        IReadOnlyList<byte[]> pages = TwoPages().ExportImages();

        Assert.Equal(2, pages.Count);
        foreach (byte[] page in pages)
        {
            // Two inches by one at the default 144 pixels per inch.
            using SKBitmap bitmap = Decode(page);
            Assert.Equal((288, 144), (bitmap.Width, bitmap.Height));
        }

        using SKBitmap small = Decode(TwoPages().ExportImages(new ImageExportOptions { Resolution = 72 })[0]);
        Assert.Equal((144, 72), (small.Width, small.Height));
    }

    private static SKBitmap Single(Extent trim, Action<IFrame> compose) =>
        Decode(Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = trim;
            section.Margins = Sides.All(0);
            compose(section.Body());
        })).ExportImages(new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Png })[0]);

    [Fact]
    public void AGradientBlendsAcrossTheFrame()
    {
        using SKBitmap page = Single(new Extent(100, 40), body => body.Fill(Gradient.Across(Ink.Rgb(255, 0, 0), Ink.Rgb(0, 0, 255))).Blank());

        Assert.True(IsNear(page.GetPixel(0, 20), new SKColor(255, 0, 0), 8), page.GetPixel(0, 20).ToString());
        Assert.True(IsNear(page.GetPixel(50, 20), new SKColor(128, 0, 128), 8), page.GetPixel(50, 20).ToString());
        Assert.True(IsNear(page.GetPixel(99, 20), new SKColor(0, 0, 255), 8), page.GetPixel(99, 20).ToString());
    }

    [Fact]
    public void ShapesAfterAGradientAreInTheirOwnInk()
    {
        using SKBitmap page = Single(new Extent(100, 40), body => body.Stack(stack =>
        {
            stack.Add().Height(20).Fill(Gradient.Across(Ink.Rgb(0, 0, 255), Ink.Rgb(0, 0, 255)));
            stack.Add().Height(20).Fill(Red);
        }));

        Assert.True(IsNear(page.GetPixel(50, 30), new SKColor(220, 20, 20), 2), page.GetPixel(50, 30).ToString());
    }

    [Theory]
    [InlineData(new float[] { 10, 10 })]
    [InlineData(new float[] { 10 })]
    public void ADashedRuleLeavesItsGapsBlank(float[] dashes)
    {
        // A pattern of one length is dash and gap alike, as in the PDF.
        using SKBitmap page = Single(new Extent(100, 10), body => body.Rule(4, Red, dashes));

        Assert.True(IsNear(page.GetPixel(5, 2), new SKColor(220, 20, 20), 8), page.GetPixel(5, 2).ToString());
        Assert.True(IsNear(page.GetPixel(15, 2), SKColors.White, 8), page.GetPixel(15, 2).ToString());
        Assert.True(IsNear(page.GetPixel(25, 2), new SKColor(220, 20, 20), 8), page.GetPixel(25, 2).ToString());
    }

    [Fact]
    public void DrawsEachFillWhereLayoutPutIt()
    {
        using SKBitmap page = Decode(TwoPages().ExportImages(new ImageExportOptions { Resolution = 72 })[0]);

        // The band covers the top half; below it is the white paper.
        Assert.True(IsNear(page.GetPixel(70, 10), new SKColor(220, 20, 20), 2), page.GetPixel(70, 10).ToString());
        Assert.True(IsNear(page.GetPixel(70, 60), SKColors.White, 2), page.GetPixel(70, 60).ToString());
    }

    [Fact]
    public void SetsTextFromTheSameGlyphsAsThePdf()
    {
        using SKBitmap page = Decode(TwoPages().ExportImages()[1]);

        int dark = 0;
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < page.Width; x++)
            {
                if (page.GetPixel(x, y).Red < 100)
                    dark++;
            }
        }

        Assert.True(dark > 100, $"Only {dark} dark pixels where the text should be.");
    }

    [Theory]
    [InlineData(PageImageFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(PageImageFormat.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData(PageImageFormat.Webp, new byte[] { 0x52, 0x49, 0x46, 0x46 })]
    public void EncodesInTheFormatAskedFor(PageImageFormat format, byte[] signature)
    {
        byte[] page = TwoPages().ExportImages(new ImageExportOptions { Format = format, Quality = 80 })[0];

        Assert.Equal(signature, page.Take(signature.Length).ToArray());
    }

    [Fact]
    public void WritesEachPageToThePathItsNumberGives()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"rustaveli-pages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            TwoPages().ExportImages(number => Path.Combine(folder, $"page-{number}.png"));

            Assert.Equal(["page-1.png", "page-2.png"], Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TurnsAnImageUprightByItsOrientation()
    {
        // Orientation 6 turns the stored image a quarter clockwise: its stored top-left corner shows at the top right.
        RasterImage image = RasterImage.FromFile(ImagePath("jpeg-exif-orientation6.jpg"));

        // The pixels as stored, decoded without the orientation applied.
        using SKCodec codec = SKCodec.Create(ImagePath("jpeg-exif-orientation6.jpg"));
        using SKBitmap stored = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888));
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(stored.Info, stored.GetPixels()));
        Assert.Equal((image.StoredWidth, image.StoredHeight), (stored.Width, stored.Height));

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(image.PixelWidth, image.PixelHeight);
            section.Body().Image(image, ImageFitting.Stretch);
        }));

        using SKBitmap page = Decode(document.ExportImages(new ImageExportOptions { Resolution = 72 })[0]);

        Assert.True(
            IsNear(page.GetPixel(page.Width - 2, 1), stored.GetPixel(1, 1), 40),
            $"Top right is {page.GetPixel(page.Width - 2, 1)}; the stored top left is {stored.GetPixel(1, 1)}.");
    }

    [Fact]
    public void RefusesAnImageItCannotDecode()
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Image(new ForeignImage())));

        RenderingException error = Assert.Throws<RenderingException>(() => document.ExportImages());

        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void RefusesOptionsOutOfRange()
    {
        ImageExportOptions options = new ImageExportOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolution = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Quality = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Quality = 101);
        Assert.Throws<ArgumentNullException>(() => ImageExport.ExportImages(null!));
        Assert.Throws<ArgumentNullException>(() => TwoPages().ExportImages((Func<int, string>)null!));
    }
}
