using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Rules N1 and N2, and I1 and I2: neutrals, then the levels every resolved type is set at.</summary>
public class NeutralTypeTests
{
    [Theory]
    [InlineData("R ON R", nameof(BidiDirection.LeftToRight), "1 1 1")]
    [InlineData("L ON L", nameof(BidiDirection.RightToLeft), "2 2 2")]
    [InlineData("R ON EN", nameof(BidiDirection.LeftToRight), "1 1 2")]
    [InlineData("R EN ON R", nameof(BidiDirection.LeftToRight), "1 2 1 1")]
    [InlineData("AN ON AN", nameof(BidiDirection.LeftToRight), "2 1 2")]
    [InlineData("R WS ON RLI PDI R", nameof(BidiDirection.LeftToRight), "1 1 1 1 1 1")]
    public void GivesNeutralsTheDirectionOnBothSidesOfThem(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("L ON R", nameof(BidiDirection.LeftToRight), "0 0 1")]
    [InlineData("L ON R", nameof(BidiDirection.RightToLeft), "2 1 1")]
    [InlineData("R ON L", nameof(BidiDirection.LeftToRight), "1 0 0")]
    public void GivesNeutralsBetweenDirectionsTheEmbeddingDirection(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("ON R", nameof(BidiDirection.RightToLeft), "1 1")]
    [InlineData("R ON", nameof(BidiDirection.LeftToRight), "1 0")]
    [InlineData("ON L", nameof(BidiDirection.RightToLeft), "1 2")]
    public void TakesTheEdgesOfTheSequenceAsTheParagraphDirection(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("L R AN EN", nameof(BidiDirection.LeftToRight), "0 1 2 2")]
    [InlineData("L R AN EN", nameof(BidiDirection.RightToLeft), "2 1 2 2")]
    public void RaisesEachTypeFromItsEmbeddingLevel(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Fact]
    public void RaisesTextOneLevelPastTheDeepestEmbedding() =>
        Assert.Equal(
            BidiClasses.Repeat("x", 63) + " 126",
            BidiClasses.Levels(BidiClasses.Repeat("LRE", 62) + " RLE L", BidiDirection.LeftToRight));
}
