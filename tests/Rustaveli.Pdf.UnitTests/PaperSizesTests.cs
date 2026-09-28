namespace Rustaveli.Pdf.UnitTests;

public class PaperSizesTests
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

    public static TheoryData<string, float, float, LengthUnit> Definitions => new TheoryData<string, float, float, LengthUnit>
    {
        { nameof(PaperSizes.A7), 74, 105, LengthUnit.Millimetre },
        { nameof(PaperSizes.A8), 52, 74, LengthUnit.Millimetre },
        { nameof(PaperSizes.A9), 37, 52, LengthUnit.Millimetre },
        { nameof(PaperSizes.A10), 26, 37, LengthUnit.Millimetre },
        { nameof(PaperSizes.B0), 1000, 1414, LengthUnit.Millimetre },
        { nameof(PaperSizes.B1), 707, 1000, LengthUnit.Millimetre },
        { nameof(PaperSizes.B2), 500, 707, LengthUnit.Millimetre },
        { nameof(PaperSizes.B3), 353, 500, LengthUnit.Millimetre },
        { nameof(PaperSizes.B4), 250, 353, LengthUnit.Millimetre },
        { nameof(PaperSizes.B5), 176, 250, LengthUnit.Millimetre },
        { nameof(PaperSizes.B6), 125, 176, LengthUnit.Millimetre },
        { nameof(PaperSizes.B7), 88, 125, LengthUnit.Millimetre },
        { nameof(PaperSizes.B8), 62, 88, LengthUnit.Millimetre },
        { nameof(PaperSizes.B9), 44, 62, LengthUnit.Millimetre },
        { nameof(PaperSizes.B10), 31, 44, LengthUnit.Millimetre },
        { nameof(PaperSizes.C0), 917, 1297, LengthUnit.Millimetre },
        { nameof(PaperSizes.C1), 648, 917, LengthUnit.Millimetre },
        { nameof(PaperSizes.C2), 458, 648, LengthUnit.Millimetre },
        { nameof(PaperSizes.C3), 324, 458, LengthUnit.Millimetre },
        { nameof(PaperSizes.C4), 229, 324, LengthUnit.Millimetre },
        { nameof(PaperSizes.C5), 162, 229, LengthUnit.Millimetre },
        { nameof(PaperSizes.C6), 114, 162, LengthUnit.Millimetre },
        { nameof(PaperSizes.C7), 81, 114, LengthUnit.Millimetre },
        { nameof(PaperSizes.C8), 57, 81, LengthUnit.Millimetre },
        { nameof(PaperSizes.C9), 40, 57, LengthUnit.Millimetre },
        { nameof(PaperSizes.C10), 28, 40, LengthUnit.Millimetre },
        { nameof(PaperSizes.EnvelopeDL), 110, 220, LengthUnit.Millimetre },
        { nameof(PaperSizes.EnvelopeNo10), 4.125f, 9.5f, LengthUnit.Inch },
        { nameof(PaperSizes.Postcard), 100, 148, LengthUnit.Millimetre },
        { nameof(PaperSizes.Ledger), 17, 11, LengthUnit.Inch },
        { nameof(PaperSizes.ArchA), 9, 12, LengthUnit.Inch },
        { nameof(PaperSizes.ArchB), 12, 18, LengthUnit.Inch },
        { nameof(PaperSizes.ArchC), 18, 24, LengthUnit.Inch },
        { nameof(PaperSizes.ArchD), 24, 36, LengthUnit.Inch },
        { nameof(PaperSizes.ArchE), 36, 48, LengthUnit.Inch },
        { nameof(PaperSizes.ArchE1), 30, 42, LengthUnit.Inch },
        { nameof(PaperSizes.ArchE2), 26, 38, LengthUnit.Inch },
        { nameof(PaperSizes.ArchE3), 27, 39, LengthUnit.Inch },
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    public void EachSizeMatchesItsStandard(string name, float width, float height, LengthUnit unit)
    {
        Extent size = (Extent)typeof(PaperSizes).GetProperty(name)!.GetValue(null)!;

        Approximately.Equal(new Extent(width.ToPoints(unit), height.ToPoints(unit)), size);
    }

    [Fact]
    public void AnEnvelopeOfTheCSeriesTakesTheSheetOfItsNumber()
    {
        Assert.True(PaperSizes.C4.Width > PaperSizes.A4.Width && PaperSizes.C4.Height > PaperSizes.A4.Height);
        Assert.True(PaperSizes.C5.Width > PaperSizes.A5.Width && PaperSizes.C5.Height > PaperSizes.A5.Height);
    }

    [Fact]
    public void LedgerIsTabloidTurnedOnItsSide() => Assert.Equal(PaperSizes.Tabloid.Landscape(), PaperSizes.Ledger);

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
