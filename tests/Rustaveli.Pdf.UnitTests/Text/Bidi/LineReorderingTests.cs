using Rustaveli.Pdf.Text.Bidi;
using static Rustaveli.Pdf.UnitTests.Bidi.BidiText;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Rules L1 and L2, on the examples UAX #9 gives for them and on lines broken out of a paragraph.</summary>
public class LineReorderingTests
{
    [Fact]
    public void ReversesRightToLeftTextInsideLeftToRightText()
    {
        Assert.Equal("00000000001110", Levels("car means CAR."));
        Assert.Equal("car means RAC.", Display("car means CAR."));
    }

    [Fact]
    public void ReversesAnIsolateAndThenTheTextInsideIt()
    {
        string text = $"{RLI}car MEANS CAR.{PDI}";

        Assert.Equal("0222111111111110", Levels(text));
        Assert.Equal(".RAC SNAEM car", Display(text));
    }

    [Fact]
    public void KeepsQuotationsIsolatedFromTheSentenceAroundThem()
    {
        string text = $"he said \u201C{RLI}car MEANS CAR{PDI}.\u201D \u201C{RLI}IT DOES{PDI},\u201D she agreed.";

        Assert.Equal("000000000022211111111110000001111111000000000000000", Levels(text));
        Assert.Equal("he said \u201CRAC SNAEM car.\u201D \u201CSEOD TI,\u201D she agreed.", Display(text));
    }

    [Fact]
    public void NestsQuotationsInsideARightToLeftParagraph()
    {
        string text = $"DID YOU SAY \u2019{LRI}he said \u201C{RLI}car MEANS CAR{PDI}\u201D{PDI}\u2018?";

        Assert.Equal("111111111111112222222222444333333333322111", Levels(text));
        Assert.Equal("?\u2018he said \u201CRAC SNAEM car\u201D\u2019 YAS UOY DID", Display(text));
    }

    [Theory]
    [InlineData("ABC\tDEF", "1110111", "CBA\tFED")]
    [InlineData("ABC \tDEF", "11100111", "CBA \tFED")]
    [InlineData("ABC\u2029", "1110", "CBA\u2029")]
    public void ReturnsSeparatorsAndTheSpaceBeforeThemToTheParagraphLevel(string text, string levels, string display)
    {
        Assert.Equal(levels, Levels(text, BidiDirection.LeftToRight));
        Assert.Equal(display, Display(text, BidiDirection.LeftToRight));
    }

    [Fact]
    public void ReturnsSpaceAndIsolateControlsEndingTheLineToTheParagraphLevel()
    {
        string text = $"abc {RLI}DEF {PDI}";

        Assert.Equal("0000011100", Levels(text));
        Assert.Equal("abc FED ", Display(text));
    }

    [Fact]
    public void ReturnsRemovedControlsEndingTheLineToTheParagraphLevel()
    {
        string text = $"{RLE}ABC {PDF}";

        Assert.Equal("011100", Levels(text, BidiDirection.LeftToRight));
        Assert.Equal("CBA ", Display(text, BidiDirection.LeftToRight));
    }

    [Fact]
    public void GivesRemovedControlsTheLevelOfTheCharacterBeforeThem()
    {
        BidiParagraph paragraph = Paragraph("ABC\u200BDEF", BidiDirection.LeftToRight);

        Assert.Equal("1111111", LineLevels(paragraph, 0, 7));
        Assert.Equal(1, paragraph.GetLevel(3));

        // At the start of a line there is no character before, so the paragraph level.
        Assert.Equal("0111", LineLevels(paragraph, 3, 4));

        // After a tab the level is the tab's once L1 has reset it.
        Assert.Equal("1001", Levels("A\t\u200BB", BidiDirection.LeftToRight));

        // At the start of the paragraph too.
        Assert.Equal(1, Paragraph("\u200BABC", BidiDirection.RightToLeft).GetLevel(0));
    }

    [Fact]
    public void AppliesTheLineRulesToEachLineAfterBreaking()
    {
        const string Text = "ABC DEF";
        BidiParagraph paragraph = Paragraph(Text, BidiDirection.LeftToRight);

        Assert.Equal("FED CBA", Display(paragraph, Text, 0, 7));
        Assert.Equal("1110", LineLevels(paragraph, 0, 4));
        Assert.Equal("CBA ", Display(paragraph, Text, 0, 4));
        Assert.Equal("FED", Display(paragraph, Text, 4, 3));
    }

    [Fact]
    public void ReordersTheLinesOfARightToLeftParagraph()
    {
        const string Text = "ABC def GHI jkl";
        BidiParagraph paragraph = Paragraph(Text);

        Assert.Equal(" def CBA", Display(paragraph, Text, 0, 8));
        Assert.Equal("jkl IHG", Display(paragraph, Text, 8, 7));
    }

    [Fact]
    public void ReversesAtLevelsTheLineSkips()
    {
        // Levels 1, 3 and 4: reversing at level 2 too, though no character has it, puts the embedded pair back in
        // right-to-left order.
        Assert.Equal("1 x 3 4 x", BidiClasses.Levels("R RLE R L PDF", BidiDirection.RightToLeft));
        Assert.Equal("3 2 0", BidiClasses.Order("R RLE R L PDF", BidiDirection.RightToLeft));
    }

    [Theory]
    [InlineData("L AN AN", "0 1 2")]
    [InlineData("R AN AN", "1 2 0")]
    public void KeepsNumbersLeftToRight(string classes, string order) =>
        Assert.Equal(order, BidiClasses.Order(classes, BidiDirection.LeftToRight));

    [Fact]
    public void MakesOneRunOfALineOnOneLevel()
    {
        List<BidiRun> runs = new List<BidiRun>();
        Paragraph("abc", BidiDirection.RightToLeft).GetVisualRuns(0, 3, runs);

        Assert.Equal([new BidiRun(0, 3, 2)], runs);
    }

    [Fact]
    public void ListsRunsInDisplayOrderWithTheirPositionsInTheParagraph()
    {
        List<BidiRun> runs = new List<BidiRun>();
        BidiParagraph paragraph = Paragraph("ab CD ef GH");

        paragraph.GetVisualRuns(0, 11, runs);
        Assert.Equal([new BidiRun(0, 3, 0), new BidiRun(3, 2, 1), new BidiRun(5, 4, 0), new BidiRun(9, 2, 1)], runs);

        paragraph.GetVisualRuns(3, 8, runs);
        Assert.Equal([new BidiRun(3, 2, 1), new BidiRun(5, 4, 0), new BidiRun(9, 2, 1)], runs);
        Assert.True(runs[0].IsRightToLeft);
        Assert.False(runs[1].IsRightToLeft);
    }
}
