namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Image sizing against a 200x100 pixel image, which is twice as wide as it is tall.
/// </summary>
public class ImageElementTests
{
    private static ImageElement Image(ImageFit fit) => new() { Image = new FakeImage(200, 100), Fit = fit };

    [Theory]
    [InlineData(ImageFit.Width, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFit.Height, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFit.Area, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFit.Area, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFit.Unproportional, 120f, 70f, 120f, 70f)]
    public void SizesTheImageByItsFit(ImageFit fit, float availableWidth, float availableHeight, float expectedWidth, float expectedHeight)
    {
        SpacePlan plan = LayoutHarness.Measure(Image(fit), new Size(availableWidth, availableHeight));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(expectedWidth, expectedHeight), plan.Size);
    }

    [Theory]
    [InlineData(ImageFit.Width, 100f, 300f, 100f, 50f)]
    [InlineData(ImageFit.Height, 300f, 40f, 80f, 40f)]
    [InlineData(ImageFit.Unproportional, 120f, 70f, 120f, 70f)]
    public void DrawsTheImageAtTheSizeItMeasured(ImageFit fit, float availableWidth, float availableHeight, float expectedWidth, float expectedHeight)
    {
        RecordedPage page = LayoutHarness.Draw(Image(fit), new Size(availableWidth, availableHeight));
        ImageOperation image = Assert.Single(page.Operations.OfType<ImageOperation>());

        Approximately.Equal(Position.Zero, image.Position);
        Approximately.Equal(new Size(expectedWidth, expectedHeight), image.Size);
    }

    [Fact]
    public void AnUnrecognisedFitFitsTheWidth()
    {
        SpacePlan plan = LayoutHarness.Measure(Image((ImageFit)99), new Size(100, 300));

        Approximately.Equal(new Size(100, 50), plan.Size);
    }

    [Theory]
    [InlineData(ImageFit.Width, 100f, 50f, false)]
    [InlineData(ImageFit.Width, 100f, 49f, true)]
    [InlineData(ImageFit.Height, 80f, 40f, false)]
    [InlineData(ImageFit.Height, 79f, 40f, true)]
    public void WrapsWhenTheFittedImageDoesNotFit(ImageFit fit, float availableWidth, float availableHeight, bool wraps)
    {
        SpacePlan plan = LayoutHarness.Measure(Image(fit), new Size(availableWidth, availableHeight));

        Assert.Equal(wraps, plan.IsWrap);
    }

    [Fact]
    public void DrawsNothingWhenTheFittedImageDoesNotFit()
    {
        // Fitting the 100pt width needs 50pt of height.
        Assert.Empty(LayoutHarness.Draw(Image(ImageFit.Width), new Size(100, 49)).Operations);
    }

    [Fact]
    public void TreatsAnImageWithNoHeightAsSquare()
    {
        ImageElement element = new ImageElement { Image = new FakeImage(50, 0), Fit = ImageFit.Width };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 300));

        Approximately.Equal(new Size(100, 100), plan.Size);
    }

    [Fact]
    public void WithoutAnImageOccupiesNothing()
    {
        ImageElement element = new ImageElement();

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 300));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Size.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(100, 300)).Operations);
    }
}
