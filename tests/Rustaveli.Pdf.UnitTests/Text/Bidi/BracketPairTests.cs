using Rustaveli.Pdf.Text.Bidi;
using static Rustaveli.Pdf.UnitTests.Bidi.BidiText;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Definition BD16 and rule N0: which brackets pair, and the direction a pair takes.</summary>
public class BracketPairTests
{
    // The examples of BD16, positions counted from 1.
    [Theory]
    [InlineData("a)b(c", "")]
    [InlineData("a(b]c", "")]
    [InlineData("a(b)c", "2-4")]
    [InlineData("a(b[c)d]", "2-6")]
    [InlineData("a(b]c)d", "2-6")]
    [InlineData("a(b)c)d", "2-4")]
    [InlineData("a(b(c)d", "4-6")]
    [InlineData("a(b(c)d)", "2-8 4-6")]
    [InlineData("a(b{c}d)", "2-8 4-6")]
    public void PairsEachClosingBracketWithTheNearestOpeningOneItCloses(string text, string pairs) =>
        Assert.Equal(pairs, Pairs(text));

    [Theory]
    [InlineData("a\u2329b\u3009")]
    [InlineData("a\u3008b\u232A")]
    public void PairsCanonicallyEquivalentAngleBrackets(string text) =>
        Assert.Equal("2-4", Pairs(text));

    [Fact]
    public void PairsOnlyBracketsThatAreStillNeutral()
    {
        BidiClass[] types = [BidiClass.L, BidiClass.L, BidiClass.L, BidiClass.ON];
        int[] closings = new int[4];

        Assert.True(BracketPairs.Find(types, [0, 1, 2, 3], "a(b)".AsSpan(), closings));
        Assert.Equal([-1, -1, -1, -1], closings);
    }

    [Fact]
    public void GivesUpPairingWhenMoreBracketsAreOpenThanTheStackHolds()
    {
        Assert.Equal("63-64", Pairs(new string('(', 63) + ")"));

        string overflowing = new string('(', 64) + ")";
        BidiClass[] types = Enumerable.Repeat(BidiClass.ON, overflowing.Length).ToArray();
        int[] closings = new int[overflowing.Length];

        Assert.False(BracketPairs.Find(types, Enumerable.Range(0, overflowing.Length).ToArray(), overflowing.AsSpan(), closings));
    }

    [Theory]
    [InlineData("AB(CD[&ef]!)gh", "gh(![ef&]DC)BA")]
    [InlineData("smith (fabrikam ARABIC) HEBREW", "WERBEH (CIBARA fabrikam) smith")]
    [InlineData("ARABIC book(s)", "book(s) CIBARA")]
    public void SetsBracketPairsAsTheSpecificationShows(string text, string display) =>
        Assert.Equal(display, Display(text, BidiDirection.RightToLeft));

    [Fact]
    public void CountsNumbersInsideBracketsAsRightToLeft() =>
        Assert.Equal("(12)BA", Display("AB(12)", BidiDirection.LeftToRight));

    [Fact]
    public void MirrorsBracketsSetRightToLeft() =>
        Assert.Equal("(DC) BA", Display("AB (CD)", BidiDirection.RightToLeft));

    [Fact]
    public void GivesNonspacingMarksAfterABracketItsDirection()
    {
        Assert.Equal("111122222222", Levels("ABC book(s)\u0301", BidiDirection.RightToLeft));
        Assert.Equal("111122222222", Levels("ABC book(\u0301s)", BidiDirection.RightToLeft));
    }

    [Fact]
    public void LeavesBracketsUnpairedPastTheStackLimitToTheNeutralRules()
    {
        string paired = Levels("ABC book" + new string('(', 63) + "s)", BidiDirection.RightToLeft);
        string unpaired = Levels("ABC book" + new string('(', 64) + "s)", BidiDirection.RightToLeft);

        Assert.Equal('2', paired[paired.Length - 1]);
        Assert.Equal('1', unpaired[unpaired.Length - 1]);
    }

    [Fact]
    public void LeavesAPairEnclosingNothingStrongToTheNeutralRules() =>
        Assert.Equal("CD( ) ba", Display("ba ( )DC", BidiDirection.RightToLeft));

    // The pairs BD16 finds in text, as 1-based positions "open-close", in the order of their opening brackets.
    private static string Pairs(string text)
    {
        BidiClass[] types = text.Select(character => BidiCharacter.ClassOf(character)).ToArray();
        int[] closings = new int[text.Length];

        Assert.True(BracketPairs.Find(types, Enumerable.Range(0, text.Length).ToArray(), text.AsSpan(), closings));

        return string.Join(
            " ",
            closings
                .Select((closing, opening) => (Opening: opening, Closing: closing))
                .Where(pair => pair.Closing >= 0)
                .Select(pair => $"{pair.Opening + 1}-{pair.Closing + 1}"));
    }
}
