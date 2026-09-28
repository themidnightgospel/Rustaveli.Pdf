using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Rules W1 to W7: nonspacing marks, and numbers with their separators and terminators.</summary>
public class WeakTypeTests
{
    [Theory]
    [InlineData("R NSM", nameof(BidiDirection.LeftToRight), "1 1")]
    [InlineData("AL NSM NSM", nameof(BidiDirection.LeftToRight), "1 1 1")]
    [InlineData("L NSM", nameof(BidiDirection.RightToLeft), "2 2")]
    [InlineData("NSM R", nameof(BidiDirection.LeftToRight), "0 1")]
    [InlineData("NSM L", nameof(BidiDirection.RightToLeft), "1 2")]
    [InlineData("R PDI NSM", nameof(BidiDirection.LeftToRight), "1 0 0")]
    public void GivesANonspacingMarkTheTypeOfWhatItFollows(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));

    [Theory]
    [InlineData("AL EN ET", "1 2 0")]
    [InlineData("AL ON EN ET", "1 1 2 0")]
    [InlineData("R EN ET", "1 2 2")]
    [InlineData("AL L EN ET", "1 0 0 0")]
    public void MakesEuropeanNumbersAfterArabicLettersArabicNumbers(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.LeftToRight));

    [Theory]
    [InlineData("EN ES EN", "2 2 2")]
    [InlineData("EN CS EN", "2 2 2")]
    [InlineData("AN CS AN", "2 2 2")]
    [InlineData("EN CS EN CS EN", "2 2 2 2 2")]
    public void JoinsNumbersAcrossASingleSeparator(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.RightToLeft));

    [Theory]
    [InlineData("AN ES AN", "2 1 2")]
    [InlineData("EN CS AN", "2 1 2")]
    [InlineData("EN ES ES EN", "2 1 1 2")]
    [InlineData("ES EN", "1 2")]
    [InlineData("EN CS", "2 1")]
    public void LeavesOtherSeparatorsNeutral(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.RightToLeft));

    [Theory]
    [InlineData("ET ET EN", "2 2 2")]
    [InlineData("EN ET ET", "2 2 2")]
    [InlineData("AN ET EN", "2 2 2")]
    [InlineData("AN ET", "2 1")]
    [InlineData("ET AN", "1 2")]
    [InlineData("ET ON EN", "1 1 2")]
    public void GivesTerminatorsBesideAEuropeanNumberToIt(string classes, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiDirection.RightToLeft));

    [Theory]
    [InlineData("L ON EN", nameof(BidiDirection.RightToLeft), "2 2 2")]
    [InlineData("R ON EN", nameof(BidiDirection.RightToLeft), "1 1 2")]
    [InlineData("L R EN", nameof(BidiDirection.LeftToRight), "0 1 2")]
    [InlineData("R L EN", nameof(BidiDirection.LeftToRight), "1 0 0")]
    public void SetsEuropeanNumbersInLeftToRightTextAsLeftToRight(string classes, string direction, string levels) =>
        Assert.Equal(levels, BidiClasses.Levels(classes, BidiClasses.Direction(direction)));
}
