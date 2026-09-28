using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Rules X1 to X10: embeddings, overrides and isolates, and the depth limit they are held to.</summary>
public class ExplicitLevelTests
{
    [Theory]
    [InlineData("L RLE R PDF L", nameof(BidiDirection.Auto), "0 x 1 x 0")]
    [InlineData("R RLE R PDF", nameof(BidiDirection.RightToLeft), "1 x 3 x")]
    public void RaisesARightToLeftEmbeddingToTheNextOddLevel(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("L LRE L PDF", nameof(BidiDirection.Auto), "0 x 2 x")]
    [InlineData("R LRE L PDF", nameof(BidiDirection.RightToLeft), "1 x 2 x")]
    public void RaisesALeftToRightEmbeddingToTheNextEvenLevel(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("RLO L EN L PDF", "x 1 1 1 x")]
    [InlineData("LRO R AN R PDF", "x 2 2 2 x")]
    public void OverridesTheDirectionOfEverythingInside(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));

    [Theory]
    [InlineData("L PDF R", "0 x 1")]
    [InlineData("RLE L PDF PDF L", "x 2 x x 0")]
    public void IgnoresAPdfWithNothingToClose(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));

    [Fact]
    public void StopsOpeningEmbeddingsAtTheDepthLimit()
    {
        // 62 LREs reach level 124 and an RLE reaches 125, the deepest; the next RLE overflows, and the first PDF closes
        // that overflow rather than the RLE that did open.
        string climb = BidiClasses.Repeat("LRE", 62) + " RLE";

        Assert.Equal(
            BidiClasses.Repeat("x", 64) + " 126 x 126 x 124",
            BidiClasses.Levels(climb + " RLE L PDF L PDF L", BidiDirection.LeftToRight));

        // An LRE at 124 would reach 126, past the limit.
        Assert.Equal(
            BidiClasses.Repeat("x", 63) + " 124",
            BidiClasses.Levels(BidiClasses.Repeat("LRE", 63) + " L", BidiDirection.LeftToRight));
    }

    [Fact]
    public void IgnoresEmbeddingsInsideAnIsolateThatOverflowed()
    {
        // The RLI overflows; the RLE inside it is not counted, so after the PDI closes the RLI, the PDF closes the valid
        // RLE at level 125 and the last L falls to 124.
        Assert.Equal(
            BidiClasses.Repeat("x", 63) + " 125 x 126 125 x 124",
            BidiClasses.Levels(
                BidiClasses.Repeat("LRE", 62) + " RLE RLI RLE L PDI PDF L", BidiDirection.LeftToRight));
    }

    [Fact]
    public void ClosesEmbeddingsLeftOpenInsideAnIsolate() =>
        Assert.Equal("0 x 4 0 0", BidiClasses.Levels("LRI RLE L PDI L", BidiDirection.LeftToRight));

    [Fact]
    public void IgnoresAPdiWithoutAnIsolate() =>
        Assert.Equal("x 2 2 2 x", BidiClasses.Levels("RLE L PDI L PDF", BidiDirection.LeftToRight));

    [Fact]
    public void KeepsAPdfFromClosingAnEmbeddingOutsideItsIsolate() =>
        Assert.Equal("x 1 x 3 1 2 x", BidiClasses.Levels("RLE LRI PDF R PDI L PDF", BidiDirection.LeftToRight));

    [Fact]
    public void GivesAParagraphSeparatorTheParagraphLevel()
    {
        // The separator falls out of the embedding into a run of its own, between two right-to-left edges.
        Assert.Equal("0 2 1 2", BidiClasses.ResolvedLevels("RLE L B L", BidiDirection.LeftToRight));
        Assert.Equal("x 2 0 2", BidiClasses.Levels("RLE L B L", BidiDirection.LeftToRight));
    }

    [Theory]
    [InlineData("FSI L ON PDI", "0 2 2 0")]
    [InlineData("FSI R ON PDI", "0 1 1 0")]
    [InlineData("FSI ON PDI R", "0 2 0 1")]
    [InlineData("FSI RLI R PDI L ON PDI", "0 2 3 2 2 2 0")]
    [InlineData("FSI R", "0 1")]
    public void DirectsAFirstStrongIsolateByTheFirstStrongCharacterInside(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));

    [Fact]
    public void OverridesIsolateControlsLikeAnyOtherCharacter() =>
        Assert.Equal(
            "x 1 x 2 x 1 2 1 x 2 x 1 x",
            BidiClasses.Levels("RLO L LRE L PDF LRI L PDI LRE L PDF L PDF"));

    [Theory]
    [InlineData("R RLI L PDI R", "1 1 2 1 1")]
    [InlineData("L RLI R PDI EN", "0 0 1 0 0")]
    public void TreatsAnIsolateAsANeutralInTheTextAroundIt(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));

    [Theory]
    [InlineData("EN BN ES BN EN", "2 x 2 x 2")]
    [InlineData("AL BN EN", "1 x 2")]
    [InlineData("R LRE PDF EN ET", "1 x x 2 2")]
    public void PassesOverRemovedControlsAsIfTheyWereNotThere(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.RightToLeft));

    [Theory]
    [InlineData("RLI R B PDI R", "0 1 0 1 1")]
    [InlineData("RLI B R PDI R", "0 0 1 1 1")]
    public void KeepsAParagraphSeparatorInsideAnIsolateFromJoiningItsEnds(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));
}
