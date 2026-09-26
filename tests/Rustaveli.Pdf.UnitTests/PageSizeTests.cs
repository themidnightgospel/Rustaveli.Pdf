namespace Rustaveli.Pdf.UnitTests;

public class PageSizeTests
{
    [Fact]
    public void A4MatchesTheIsoDefinition()
    {
        Approximately.Equal(595.28f, PaperSizes.A4.Width);
        Approximately.Equal(841.89f, PaperSizes.A4.Height);
    }

    [Fact]
    public void TheIsoASeriesMatchesItsMillimetreDefinitions()
    {
        Approximately.Equal(new Extent(2383.94f, 3370.39f), PaperSizes.A0);
        Approximately.Equal(new Extent(1683.78f, 2383.94f), PaperSizes.A1);
        Approximately.Equal(new Extent(1190.55f, 1683.78f), PaperSizes.A2);
        Approximately.Equal(new Extent(841.89f, 1190.55f), PaperSizes.A3);
        Approximately.Equal(new Extent(419.53f, 595.28f), PaperSizes.A5);
        Approximately.Equal(new Extent(297.64f, 419.53f), PaperSizes.A6);
    }

    [Fact]
    public void NorthAmericanSizesMatchTheirInchDefinitions()
    {
        Approximately.Equal(new Extent(612f, 792f), PaperSizes.Letter);
        Approximately.Equal(new Extent(612f, 1008f), PaperSizes.Legal);
        Approximately.Equal(new Extent(792f, 1224f), PaperSizes.Tabloid);
        Approximately.Equal(new Extent(522f, 756f), PaperSizes.Executive);
    }

    [Fact]
    public void LandscapeSwapsTheAxes()
    {
        Extent landscape = PaperSizes.A4.Landscape();

        Approximately.Equal(PaperSizes.A4.Height, landscape.Width);
        Approximately.Equal(PaperSizes.A4.Width, landscape.Height);
    }

    [Fact]
    public void PortraitLeavesAnAlreadyPortraitSizeAlone()
    {
        Assert.Equal(PaperSizes.A4, PaperSizes.A4.Portrait());
    }

    [Fact]
    public void PortraitTurnsALandscapeSizeUpright()
    {
        Assert.Equal(new Extent(200, 300), new Extent(300, 200).Portrait());
    }

    [Fact]
    public void EachSizeIsHalfTheNextLargest()
    {
        // A5 is A4 folded in half, so its long edge equals A4's short edge.
        Approximately.Equal(PaperSizes.A4.Width, PaperSizes.A5.Height);
    }
}
