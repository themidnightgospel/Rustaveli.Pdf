namespace Rustaveli.Pdf.UnitTests;

public class TextStyleTests
{
    private static readonly TextStyle Base = TextStyle.Default;

    [Fact]
    public void DefaultIsTwelvePointBlackHelveticaWithNoDecoration()
    {
        TextStyle style = TextStyle.Default;

        Assert.Equal("Helvetica", style.FontFamily);
        Assert.Equal(12f, style.FontSize);
        Assert.Equal(FontWeight.Normal, style.Weight);
        Assert.False(style.IsItalic);
        Assert.Equal(TestInks.Black, style.Color);
        Assert.Equal(TestInks.Transparent, style.BackgroundColor);
        Assert.False(style.HasUnderline);
        Assert.False(style.HasStrikethrough);
        Assert.Equal(1f, style.LineHeight);
        Assert.Equal(0f, style.LetterSpacing);
        Assert.Equal(FontPosition.Normal, style.Position);
    }

    [Fact]
    public void FontFamilyOfChangesOnlyTheFamily()
    {
        Assert.Equal(Base with { FontFamily = "Georgia" }, Base.FontFamilyOf("Georgia"));
    }

    [Fact]
    public void FontSizeOfChangesOnlyTheSize()
    {
        Assert.Equal(Base with { FontSize = 20 }, Base.FontSizeOf(20));
    }

    [Fact]
    public void WeightOfChangesOnlyTheWeight()
    {
        Assert.Equal(Base with { Weight = FontWeight.Light }, Base.WeightOf(FontWeight.Light));
    }

    [Fact]
    public void BoldSetsTheBoldWeight()
    {
        Assert.Equal(Base with { Weight = FontWeight.Bold }, Base.Bold());
    }

    [Fact]
    public void ItalicTurnsItalicOnByDefaultAndCanTurnItOffAgain()
    {
        Assert.Equal(Base with { IsItalic = true }, Base.Italic());
        Assert.Equal(Base, Base.Italic().Italic(false));
    }

    [Fact]
    public void ColorOfChangesOnlyTheTextColour()
    {
        Assert.Equal(Base with { Color = TestInks.Red }, Base.ColorOf(TestInks.Red));
    }

    [Fact]
    public void BackgroundColorOfChangesOnlyTheHighlight()
    {
        Assert.Equal(Base with { BackgroundColor = TestInks.Yellow }, Base.BackgroundColorOf(TestInks.Yellow));
    }

    [Fact]
    public void UnderlineTurnsUnderliningOnByDefaultAndCanTurnItOffAgain()
    {
        Assert.Equal(Base with { HasUnderline = true }, Base.Underline());
        Assert.Equal(Base, Base.Underline().Underline(false));
    }

    [Fact]
    public void StrikethroughTurnsStrikingOnByDefaultAndCanTurnItOffAgain()
    {
        Assert.Equal(Base with { HasStrikethrough = true }, Base.Strikethrough());
        Assert.Equal(Base, Base.Strikethrough().Strikethrough(false));
    }

    [Fact]
    public void LineHeightOfChangesOnlyTheLineHeight()
    {
        Assert.Equal(Base with { LineHeight = 1.5f }, Base.LineHeightOf(1.5f));
    }

    [Fact]
    public void LetterSpacingOfChangesOnlyTheLetterSpacing()
    {
        Assert.Equal(Base with { LetterSpacing = 2f }, Base.LetterSpacingOf(2f));
    }

    [Fact]
    public void SubscriptAndSuperscriptChangeOnlyThePosition()
    {
        Assert.Equal(Base with { Position = FontPosition.Subscript }, Base.Subscript());
        Assert.Equal(Base with { Position = FontPosition.Superscript }, Base.Superscript());
    }

    [Fact]
    public void MutatorsLeaveTheStyleTheyWereCalledOnUntouched()
    {
        // The default is shared by every span in a document, so deriving a variant must never alter it.
        TextStyle original = TextStyle.Default.FontSizeOf(10);

        original.Bold().Italic().Underline().ColorOf(TestInks.Red).FontSizeOf(30).Superscript();

        Assert.Equal(TextStyle.Default with { FontSize = 10 }, original);
    }

    [Fact]
    public void StylesReachedByDifferentRoutesAreEqual()
    {
        TextStyle first = Base.Bold().FontSizeOf(14);
        TextStyle second = Base.FontSizeOf(14).WeightOf(FontWeight.Bold);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void NormalTextRendersAtItsNominalSizeOnTheBaseline()
    {
        TextStyle style = Base.FontSizeOf(20);

        Assert.Equal(20f, style.EffectiveFontSize);
        Assert.Equal(0f, style.BaselineOffset);
    }

    [Fact]
    public void SubscriptShrinksAndDropsBelowTheBaseline()
    {
        TextStyle style = Base.FontSizeOf(20).Subscript();

        Approximately.Equal(11.6f, style.EffectiveFontSize);
        Approximately.Equal(3.2f, style.BaselineOffset);
    }

    [Fact]
    public void SuperscriptShrinksAndRisesAboveTheBaseline()
    {
        TextStyle style = Base.FontSizeOf(20).Superscript();

        Approximately.Equal(11.6f, style.EffectiveFontSize);
        Approximately.Equal(-6.6f, style.BaselineOffset);
    }

    [Fact]
    public void ScriptOffsetsScaleWithTheNominalNotTheShrunkenSize()
    {
        // The shift is measured against the surrounding text, not the shrunken glyphs: 40pt text drops its
        // subscript by 6.4 points, not by the 3.7 that 16% of the reduced size would give.
        Approximately.Equal(6.4f, Base.FontSizeOf(40).Subscript().BaselineOffset);
        Approximately.Equal(-13.2f, Base.FontSizeOf(40).Superscript().BaselineOffset);
    }
}
