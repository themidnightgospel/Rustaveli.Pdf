using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.UnitTests.Images;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Images made what their settings, or the export's, ask for where they are shown.
/// </summary>
public class ImageAdjusterTests
{
    private static readonly RasterImage Wide = RasterImage.FromBytes(PngWriter.Rgb(400, 200, new byte[400 * 200 * 3]));

    /// <summary>Records what it is asked and answers with a one-pixel PNG.</summary>
    private sealed class Recorder : IImageProcessor
    {
        public List<ImageProcessing> Requests { get; } = [];

        public byte[] Process(ImageProcessing request)
        {
            Requests.Add(request);
            return PngWriter.Rgb(request.PixelWidth, request.PixelHeight, new byte[request.PixelWidth * request.PixelHeight * 3]);
        }
    }

    [Fact]
    public void AnImageAskingForNothingIsLeftAsItIs()
    {
        Recorder processor = new Recorder();

        Assert.Same(Wide, new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor }).Adjust(Wide, new Extent(10, 5)));
        Assert.Same(Wide, new ImageAdjuster(null).Adjust(Wide, new Extent(10, 5)));
        Assert.Empty(processor.Requests);
    }

    [Fact]
    public void AnImageShownSmallerThanItsPixelsNeedIsScaledDown()
    {
        Recorder processor = new Recorder();
        ImageAdjuster adjuster = new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor });

        RasterImage adjusted = adjuster.Adjust(Wide.WithMaximumResolution(144), new Extent(100, 50));

        // 144 pixels an inch over 100 by 50 points: 200 by 100 pixels.
        Assert.Equal((200, 100), (adjusted.PixelWidth, adjusted.PixelHeight));
        Assert.Equal(new ImageProcessing(Wide.Source, 200, 100, null), Assert.Single(processor.Requests) with { Source = Wide.Source });
    }

    [Fact]
    public void AnImageShownLargerThanItsPixelsIsLeftWhole()
    {
        Recorder processor = new Recorder();

        RasterImage adjusted = new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor, MaximumImageResolution = 300 }).Adjust(Wide, new Extent(500, 250));

        Assert.Same(Wide, adjusted);
    }

    [Fact]
    public void AQualityRecompressesAtTheImagesOwnSize()
    {
        Recorder processor = new Recorder();

        new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor, ImageQuality = 60 }).Adjust(Wide, new Extent(10, 5));

        ImageProcessing request = Assert.Single(processor.Requests);
        Assert.Equal((400, 200, (int?)60), (request.PixelWidth, request.PixelHeight, request.Quality));
    }

    [Fact]
    public void TheImagesOwnSettingsComeBeforeTheExports()
    {
        Recorder processor = new Recorder();
        ImageAdjuster adjuster = new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor, ImageQuality = 60, MaximumImageResolution = 36 });

        adjuster.Adjust(Wide.WithQuality(90).WithMaximumResolution(72), new Extent(100, 50));

        ImageProcessing request = Assert.Single(processor.Requests);
        Assert.Equal((100, 50, (int?)90), (request.PixelWidth, request.PixelHeight, request.Quality));
    }

    [Fact]
    public void TheSameImageAtTheSameSizeIsProcessedOnce()
    {
        Recorder processor = new Recorder();
        ImageAdjuster adjuster = new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor, ImageQuality = 60 });

        RasterImage first = adjuster.Adjust(Wide, new Extent(10, 5));
        RasterImage second = adjuster.Adjust(RasterImage.FromBytes(PngWriter.Rgb(400, 200, new byte[400 * 200 * 3])), new Extent(10, 5));

        Assert.Same(first, second);
        Assert.Single(processor.Requests);
    }

    [Theory]
    [InlineData(null, 95)]
    [InlineData(60, 60)]
    public void ACmykImageUnderPdfABecomesRgbFinelyCompressedUnlessAskedOtherwise(int? quality, int expected)
    {
        Recorder processor = new Recorder();
        RasterImage cmyk = RasterImage.FromBytes(TestJpeg.Build(TestJpeg.Frame(40, 20, 4)));
        ImageAdjuster adjuster = new ImageAdjuster(new PdfExportOptions { ImageProcessor = processor, ImageQuality = quality, Conformance = PdfAConformance.PdfA2B });

        adjuster.Adjust(cmyk, new Extent(40, 20));

        ImageProcessing request = Assert.Single(processor.Requests);
        Assert.Equal((40, 20, (int?)expected), (request.PixelWidth, request.PixelHeight, request.Quality));
    }

    [Fact]
    public void AskingWithoutAProcessorSaysWhatIsNeeded()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new ImageAdjuster(new PdfExportOptions()).Adjust(Wide.WithQuality(50), new Extent(10, 5)));

        Assert.Contains("SkiaImageProcessor", exception.Message);
    }

    [Fact]
    public void SettingsAreKeptAndThePixelsShared()
    {
        RasterImage adjusted = Wide.WithQuality(40).WithMaximumResolution(150);

        Assert.Equal((40, 150f), (adjusted.Quality, adjusted.MaximumResolution));
        Assert.Equal((Wide.PixelWidth, Wide.PixelHeight), (adjusted.PixelWidth, adjusted.PixelHeight));
        Assert.True(adjusted.HasSameContent(Wide));
        Assert.Null(Wide.Quality);
        Assert.Null(Wide.MaximumResolution);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AQualityRunsFromOneToAHundred(int quality)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Wide.WithQuality(quality));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfExportOptions { ImageQuality = quality });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AResolutionIsAboveNothing(float resolution)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Wide.WithMaximumResolution(resolution));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfExportOptions { MaximumImageResolution = resolution });
    }

    [Fact]
    public void TheExportAsksForNothingUnlessSet()
    {
        PdfExportOptions options = new PdfExportOptions { ImageQuality = 70, MaximumImageResolution = 200 };
        options.ImageQuality = null;
        options.MaximumImageResolution = null;

        Assert.Equal((null, null, null), (options.ImageQuality, options.MaximumImageResolution, options.ImageProcessor));
    }
}
