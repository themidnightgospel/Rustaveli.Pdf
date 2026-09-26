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
    public void TheIsoASeriesMatchesItsMillimetreDefinitions()
    {
        Approximately.Equal(new Size(2383.94f, 3370.39f), PageSizes.A0);
        Approximately.Equal(new Size(1683.78f, 2383.94f), PageSizes.A1);
        Approximately.Equal(new Size(1190.55f, 1683.78f), PageSizes.A2);
        Approximately.Equal(new Size(841.89f, 1190.55f), PageSizes.A3);
        Approximately.Equal(new Size(419.53f, 595.28f), PageSizes.A5);
        Approximately.Equal(new Size(297.64f, 419.53f), PageSizes.A6);
    }

    [Fact]
    public void NorthAmericanSizesMatchTheirInchDefinitions()
    {
        Approximately.Equal(new Size(612f, 792f), PageSizes.Letter);
        Approximately.Equal(new Size(612f, 1008f), PageSizes.Legal);
        Approximately.Equal(new Size(792f, 1224f), PageSizes.Tabloid);
        Approximately.Equal(new Size(522f, 756f), PageSizes.Executive);
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
    public void PortraitTurnsALandscapeSizeUpright()
    {
        Assert.Equal(new Size(200, 300), new Size(300, 200).Portrait());
    }

    [Fact]
    public void EachSizeIsHalfTheNextLargest()
    {
        // A5 is A4 folded in half, so its long edge equals A4's short edge.
        Approximately.Equal(PageSizes.A4.Width, PageSizes.A5.Height);
    }
}
