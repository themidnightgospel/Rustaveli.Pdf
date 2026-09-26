namespace Rustaveli.Pdf.UnitTests;

public class TextStyleTests
{
    private static readonly TypeStyle Base = TypeStyle.Default;

    [Fact]
    public void DefaultIsTwelvePointBlackHelveticaWithNoDecoration()
    {
        TypeStyle style = TypeStyle.Default;

        Assert.Equal("Helvetica", style.Typeface);
        Assert.Equal(12f, style.PointSize);
        Assert.Equal(TypeWeight.Normal, style.Weight);
        Assert.False(style.IsItalic);
        Assert.Equal(TestInks.Black, style.Ink);
        Assert.Equal(TestInks.Transparent, style.Highlight);
        Assert.False(style.HasUnderline);
        Assert.False(style.HasStrikeThrough);
        Assert.Equal(1f, style.Leading);
        Assert.Equal(0f, style.Tracking);
        Assert.Equal(ScriptPosition.Normal, style.Script);
    }

    [Fact]
    public void FontFamilyOfChangesOnlyTheFamily()
    {
        Assert.Equal(Base with { Typeface = "Georgia" }, Base.WithTypeface("Georgia"));
    }

    [Fact]
    public void FontSizeOfChangesOnlyTheSize()
    {
        Assert.Equal(Base with { PointSize = 20 }, Base.WithPointSize(20));
    }

    [Fact]
    public void WeightOfChangesOnlyTheWeight()
    {
        Assert.Equal(Base with { Weight = TypeWeight.Light }, Base.WithWeight(TypeWeight.Light));
    }

    [Fact]
    public void BoldSetsTheBoldWeight()
    {
        Assert.Equal(Base with { Weight = TypeWeight.Bold }, Base.Bold());
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
        Assert.Equal(Base with { Ink = TestInks.Red }, Base.WithInk(TestInks.Red));
    }

    [Fact]
    public void BackgroundColorOfChangesOnlyTheHighlight()
    {
        Assert.Equal(Base with { Highlight = TestInks.Yellow }, Base.WithHighlight(TestInks.Yellow));
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
        Assert.Equal(Base with { HasStrikeThrough = true }, Base.StrikeThrough());
        Assert.Equal(Base, Base.StrikeThrough().StrikeThrough(false));
    }

    [Fact]
    public void LineHeightOfChangesOnlyTheLineHeight()
    {
        Assert.Equal(Base with { Leading = 1.5f }, Base.WithLeading(1.5f));
    }

    [Fact]
    public void LetterSpacingOfChangesOnlyTheLetterSpacing()
    {
        Assert.Equal(Base with { Tracking = 2f }, Base.WithTracking(2f));
    }

    [Fact]
    public void SubscriptAndSuperscriptChangeOnlyThePosition()
    {
        Assert.Equal(Base with { Script = ScriptPosition.Subscript }, Base.Subscript());
        Assert.Equal(Base with { Script = ScriptPosition.Superscript }, Base.Superscript());
    }

    [Fact]
    public void MutatorsLeaveTheStyleTheyWereCalledOnUntouched()
    {
        // The default is shared by every span in a document, so deriving a variant must never alter it.
        TypeStyle original = TypeStyle.Default.WithPointSize(10);

        original.Bold().Italic().Underline().WithInk(TestInks.Red).WithPointSize(30).Superscript();

        Assert.Equal(TypeStyle.Default with { PointSize = 10 }, original);
    }

    [Fact]
    public void StylesReachedByDifferentRoutesAreEqual()
    {
        TypeStyle first = Base.Bold().WithPointSize(14);
        TypeStyle second = Base.WithPointSize(14).WithWeight(TypeWeight.Bold);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void NormalTextRendersAtItsNominalSizeOnTheBaseline()
    {
        TypeStyle style = Base.WithPointSize(20);

        Assert.Equal(20f, style.EffectivePointSize);
        Assert.Equal(0f, style.BaselineOffset);
    }

    [Fact]
    public void SubscriptShrinksAndDropsBelowTheBaseline()
    {
        TypeStyle style = Base.WithPointSize(20).Subscript();

        Approximately.Equal(11.6f, style.EffectivePointSize);
        Approximately.Equal(3.2f, style.BaselineOffset);
    }

    [Fact]
    public void SuperscriptShrinksAndRisesAboveTheBaseline()
    {
        TypeStyle style = Base.WithPointSize(20).Superscript();

        Approximately.Equal(11.6f, style.EffectivePointSize);
        Approximately.Equal(-6.6f, style.BaselineOffset);
    }

    [Fact]
    public void ScriptOffsetsScaleWithTheNominalNotTheShrunkenSize()
    {
        // The shift is measured against the surrounding text, not the shrunken glyphs: 40pt text drops its
        // subscript by 6.4 points, not by the 3.7 that 16% of the reduced size would give.
        Approximately.Equal(6.4f, Base.WithPointSize(40).Subscript().BaselineOffset);
        Approximately.Equal(-13.2f, Base.WithPointSize(40).Superscript().BaselineOffset);
    }
}
