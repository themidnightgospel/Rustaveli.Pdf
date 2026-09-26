using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>Rules P2 and P3, and HL1: the paragraph's direction.</summary>
public class ParagraphLevelTests
{
    [Theory]
    [InlineData("L R", 0)]
    [InlineData("R L", 1)]
    [InlineData("AL L", 1)]
    [InlineData("ON EN WS R L", 1)]
    [InlineData("AN EN L", 0)]
    [InlineData("ON EN AN", 0)]
    public void TakesTheDirectionOfTheFirstStrongCharacter(string classes, int level) =>
        Assert.Equal(level, BidiClasses.Paragraph(classes).ParagraphLevel);

    [Theory]
    [InlineData("RLI L PDI R", 1)]
    [InlineData("LRI R PDI L", 0)]
    [InlineData("FSI R PDI ON", 0)]
    [InlineData("RLI LRI L PDI L PDI R", 1)]
    public void PassesOverIsolates(string classes, int level) =>
        Assert.Equal(level, BidiClasses.Paragraph(classes).ParagraphLevel);

    [Theory]
    [InlineData("LRI R")]
    [InlineData("RLI R PDI LRI R")]
    public void PassesOverAnIsolateWithoutItsPdiToTheEndOfTheParagraph(string classes) =>
        Assert.Equal(0, BidiClasses.Paragraph(classes).ParagraphLevel);

    [Fact]
    public void CountsWhatEmbeddingsHoldButNotTheEmbeddingsThemselves() =>
        Assert.Equal(1, BidiClasses.Paragraph("LRE R PDF L").ParagraphLevel);

    [Fact]
    public void IsLeftToRightWithoutAStrongCharacter()
    {
        Assert.Equal(0, BidiClasses.Paragraph("ON WS EN AN PDI").ParagraphLevel);
        Assert.Equal(0, BidiText.Paragraph(string.Empty).ParagraphLevel);
    }

    [Fact]
    public void TakesAGivenDirectionWhateverTheTextHolds()
    {
        Assert.Equal(0, BidiClasses.Paragraph("R", BidiDirection.LeftToRight).ParagraphLevel);
        Assert.Equal("1", BidiClasses.Levels("R", BidiDirection.LeftToRight));

        Assert.Equal(1, BidiClasses.Paragraph("L", BidiDirection.RightToLeft).ParagraphLevel);
        Assert.Equal("2", BidiClasses.Levels("L", BidiDirection.RightToLeft));
        Assert.Equal(1, BidiText.Paragraph(string.Empty, BidiDirection.RightToLeft).ParagraphLevel);
    }
}
