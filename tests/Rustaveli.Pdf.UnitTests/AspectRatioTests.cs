namespace Rustaveli.Pdf.UnitTests;

public class AspectRatioTests
{
    [Fact]
    public void DerivesHeightFromWidth()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitWidth };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void DerivesWidthFromHeight()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitHeight };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 50));

        Approximately.Equal(new Size(100, 50), plan.Size);
    }

    [Fact]
    public void FallsBackToHeightWhenWidthWouldOverflow()
    {
        // Fitting the 300pt width would need 300pt of height, but only 100 is available.
        AspectRatioElement element = new AspectRatioElement { Ratio = 1f, Option = AspectRatioOption.FitArea };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(300, 100));

        Approximately.Equal(new Size(100, 100), plan.Size);
    }

    [Fact]
    public void RejectsANonPositiveRatio()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 0f };

        Assert.True(LayoutHarness.Measure(element, new Size(100, 100)).IsWrap);
    }
}
