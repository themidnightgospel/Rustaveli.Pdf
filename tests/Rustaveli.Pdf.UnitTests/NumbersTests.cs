using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests;

public class NumbersTests
{
    private const string ResolutionMessage = "A resolution is a finite number of pixels per inch above nothing.";
    private const string QualityMessage = "Quality runs from 1 to 100.";

    [Theory]
    [InlineData(0f, true)]
    [InlineData(-1e30f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    [InlineData(float.NegativeInfinity, false)]
    public void ASingleIsFiniteUnlessNaNOrInfinite(float value, bool finite) =>
        Assert.Equal(finite, Numbers.IsFinite(value));

    [Theory]
    [InlineData(0d, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    public void ADoubleIsFiniteUnlessNaNOrInfinite(double value, bool finite) =>
        Assert.Equal(finite, Numbers.IsFinite(value));

    [Theory]
    [InlineData(0f)]
    [InlineData(-72f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AResolutionIsAFiniteNumberAboveNothing(float resolution)
    {
        ArgumentOutOfRangeException error =
            Assert.Throws<ArgumentOutOfRangeException>(() => Numbers.Resolution(resolution, "pixelsPerInch"));

        Assert.Equal("pixelsPerInch", error.ParamName);
        Assert.Equal(resolution, error.ActualValue);
        Assert.StartsWith(ResolutionMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AResolutionAboveNothingIsGivenBack() =>
        Assert.Equal(1e30f, Numbers.Resolution(1e30f, "value"));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void QualityRunsFromOneToAHundred(int quality)
    {
        ArgumentOutOfRangeException error =
            Assert.Throws<ArgumentOutOfRangeException>(() => Numbers.Quality(quality, "quality"));

        Assert.Equal("quality", error.ParamName);
        Assert.Equal(quality, error.ActualValue);
        Assert.StartsWith(QualityMessage, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void AQualityFromOneToAHundredIsGivenBack(int quality) =>
        Assert.Equal(quality, Numbers.Quality(quality, "value"));

    /// <summary>Each check of a resolution or a quality says the same, naming the parameter its caller takes.</summary>
    [Fact]
    public void EveryResolutionAndQualityCheckSaysTheSame()
    {
        RasterImage image = RasterImage.FromBytes(PngWriter.Rgb(1, 1, [0, 0, 0]));

        AssertRefused(() => new PdfExportOptions { ImageResolution = 0 }, "value", 0f, ResolutionMessage);
        AssertRefused(() => new PdfExportOptions { MaximumImageResolution = 0 }, "value", 0f, ResolutionMessage);
        AssertRefused(() => image.WithMaximumResolution(0), "pixelsPerInch", 0f, ResolutionMessage);
        AssertRefused(() => new PdfExportOptions { ImageQuality = 0 }, "value", 0, QualityMessage);
        AssertRefused(() => image.WithQuality(101), "quality", 101, QualityMessage);
    }

    private static void AssertRefused(Action refused, string name, object value, string message)
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(refused);

        Assert.Equal(name, error.ParamName);
        Assert.Equal(value, error.ActualValue);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }
}
