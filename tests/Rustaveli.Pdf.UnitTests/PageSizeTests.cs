namespace Rustaveli.Pdf.UnitTests;

public class PageSizeTests
{
    [Fact]
    public void A4MatchesTheIsoDefinition()
    {
        Approximately.Equal(595.28f, PageSizes.A4.Width);
        Approximately.Equal(841.89f, PageSizes.A4.Height);
    }

    [Fact]
    public void LandscapeSwapsTheAxes()
    {
        Size landscape = PageSizes.A4.Landscape();

        Approximately.Equal(PageSizes.A4.Height, landscape.Width);
        Approximately.Equal(PageSizes.A4.Width, landscape.Height);
    }

    [Fact]
    public void PortraitLeavesAnAlreadyPortraitSizeAlone()
    {
        Assert.Equal(PageSizes.A4, PageSizes.A4.Portrait());
    }

    [Fact]
    public void EachSizeIsHalfTheNextLargest()
    {
        // A5 is A4 folded in half, so its long edge equals A4's short edge.
        Approximately.Equal(PageSizes.A4.Width, PageSizes.A5.Height);
    }
}
