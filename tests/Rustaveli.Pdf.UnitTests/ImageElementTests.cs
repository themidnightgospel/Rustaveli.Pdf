namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Image sizing against a 200x100 pixel image, which is twice as wide as it is tall.
/// </summary>
public class ImageElementTests
{
    private static ImageBlock Image(ImageFitting fit) => new() { Image = new FakeImage(200, 100), Fit = fit };

    [Theory]
    [InlineData(ImageFitting.Width, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFitting.Height, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFitting.Area, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFitting.Area, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFitting.Unproportional, 120f, 70f, 120f, 70f)]
    public void SizesTheImageByItsFit(ImageFitting fit, float availableWidth, float availableHeight, float expectedWidth, float expectedHeight)
    {
        Fit plan = LayoutHarness.Measure(Image(fit), new Extent(availableWidth, availableHeight));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(expectedWidth, expectedHeight), plan.Size);
    }

    [Theory]
    [InlineData(ImageFitting.Width, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFitting.Height, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFitting.Unproportional, 120f, 70f, 120f, 70f)]
    public void DrawsTheImageAtTheSizeItMeasured(ImageFitting fit, float availableWidth, float availableHeight, float expectedWidth, float expectedHeight)
    {
        RecordedPage page = LayoutHarness.Draw(Image(fit), new Extent(availableWidth, availableHeight));
        ImageOperation image = Assert.Single(page.Operations.OfType<ImageOperation>());

        Approximately.Equal(Offset.Zero, image.Position);
        Approximately.Equal(new Extent(expectedWidth, expectedHeight), image.Size);
    }

    [Fact]
    public void AnUnrecognisedFitFitsTheWidth()
    {
        Fit plan = LayoutHarness.Measure(Image((ImageFitting)99), new Extent(100, 300));

        Approximately.Equal(new Extent(100, 50), plan.Size);
    }

    [Theory]
    [InlineData(ImageFitting.Width, 100f, 50f, false)]
    [InlineData(ImageFitting.Width, 100f, 49f, true)]
    [InlineData(ImageFitting.Height, 80f, 40f, false)]
    [InlineData(ImageFitting.Height, 79f, 40f, true)]
    public void WrapsWhenTheFittedImageDoesNotFit(ImageFitting fit, float availableWidth, float availableHeight, bool wraps)
    {
        Fit plan = LayoutHarness.Measure(Image(fit), new Extent(availableWidth, availableHeight));

        Assert.Equal(wraps, plan.IsWrap);
    }

    [Fact]
    public void DrawsNothingWhenTheFittedImageDoesNotFit()
    {
        // Fitting the 100pt width needs 50pt of height.
        Assert.Empty(LayoutHarness.Draw(Image(ImageFitting.Width), new Extent(100, 49)).Operations);
    }

    [Fact]
    public void TreatsAnImageWithNoHeightAsSquare()
    {
        ImageBlock element = new ImageBlock { Image = new FakeImage(50, 0), Fit = ImageFitting.Width };

        Fit plan = LayoutHarness.Measure(element, new Extent(100, 300));

        Approximately.Equal(new Extent(100, 100), plan.Size);
    }

    [Fact]
    public void WithoutAnImageOccupiesNothing()
    {
        ImageBlock element = new ImageBlock();

        Fit plan = LayoutHarness.Measure(element, new Extent(100, 300));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(100, 300)).Operations);
    }
}
