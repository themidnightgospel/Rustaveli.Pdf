namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Span refinements, observed as the style the canvas is actually asked to draw with.
/// </summary>
public class RunComposerTests
{
    /// <summary>
    /// Draws a one-span paragraph and returns the style of the single run it produced.
    /// </summary>
    /// <param name="refine">The refinements under test.</param>
    /// <param name="inherited">The surrounding default, so a refinement can be seen overriding it.</param>
    private static TypeStyle StyleOf(
        Func<RunComposer, RunComposer> refine,
        Func<TypeStyle, TypeStyle>? inherited = null)
    {
        Block root = LayoutHarness.Build(container => container
            .DefaultType(inherited ?? (style => style))
            .Text(text => refine(text.Run("x"))));

        return Assert.Single(LayoutHarness.Draw(root, new Extent(200, 200)).Texts).Style;
    }

    [Fact]
    public void TypefaceSetsTheTypeface() =>
        Assert.Equal("Courier", StyleOf(span => span.Typeface("Courier")).Typeface);

    [Fact]
    public void PointSizeSetsTheSize() =>
        Approximately.Equal(20f, StyleOf(span => span.PointSize(20)).PointSize);

    [Fact]
    public void FontColorSetsTheColour() =>
        Assert.Equal((Ink)TestInks.Red, StyleOf(span => span.Ink(TestInks.Red)).Ink);

    [Fact]
    public void FontColorAcceptsHex() =>
        Assert.Equal(Ink.Rgb(0x33, 0x66, 0x99), StyleOf(span => span.Ink("#336699")).Ink);

    [Fact]
    public void HighlightSetsTheHighlight() =>
        Assert.Equal((Ink)TestInks.Yellow, StyleOf(span => span.Highlight(TestInks.Yellow)).Highlight);

    [Fact]
    public void HighlightAcceptsHex() =>
        Assert.Equal(Ink.Rgb(0xFF, 0xFF, 0x00), StyleOf(span => span.Highlight("#FFFF00")).Highlight);

    [Fact]
    public void WeightSetsAnyWeight() =>
        Assert.Equal(TypeWeight.Light, StyleOf(span => span.Weight(TypeWeight.Light)).Weight);

    [Fact]
    public void BoldSetsTheBoldWeight() =>
        Assert.Equal(TypeWeight.Bold, StyleOf(span => span.Bold()).Weight);

    [Fact]
    public void ItalicWithoutAnArgumentSwitchesItalicOn() =>
        Assert.True(StyleOf(span => span.Italic()).IsItalic);

    [Fact]
    public void UnderlineWithoutAnArgumentSwitchesUnderlineOn() =>
        Assert.True(StyleOf(span => span.Underline()).HasUnderline);

    [Fact]
    public void StrikethroughWithoutAnArgumentSwitchesStrikethroughOn() =>
        Assert.True(StyleOf(span => span.StrikeThrough()).HasStrikeThrough);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ItalicOverridesTheInheritedSetting(bool value) =>
        Assert.Equal(value, StyleOf(span => span.Italic(value), style => style.Italic(!value)).IsItalic);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnderlineOverridesTheInheritedSetting(bool value) =>
        Assert.Equal(value, StyleOf(span => span.Underline(value), style => style.Underline(!value)).HasUnderline);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StrikethroughOverridesTheInheritedSetting(bool value) =>
        Assert.Equal(
            value,
            StyleOf(span => span.StrikeThrough(value), style => style.StrikeThrough(!value)).HasStrikeThrough);

    [Fact]
    public void LeadingSetsTheMultiplier() =>
        Approximately.Equal(1.5f, StyleOf(span => span.Leading(1.5f)).Leading);

    [Fact]
    public void TrackingSetsTheGap() =>
        Approximately.Equal(2f, StyleOf(span => span.Tracking(2)).Tracking);

    [Fact]
    public void SubscriptLowersTheRun() =>
        Assert.Equal(ScriptPosition.Subscript, StyleOf(span => span.Subscript()).Script);

    [Fact]
    public void SuperscriptRaisesTheRun() =>
        Assert.Equal(ScriptPosition.Superscript, StyleOf(span => span.Superscript()).Script);

    [Fact]
    public void StyleAppliesAnArbitraryTransformation()
    {
        TypeStyle style = StyleOf(span => span.Style(inherited => inherited.WithPointSize(30).Bold()));

        Approximately.Equal(30f, style.PointSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void ChainedRefinementsAllApply()
    {
        TypeStyle style = StyleOf(span => span.Bold().PointSize(18).Italic());

        Assert.Equal(TypeWeight.Bold, style.Weight);
        Approximately.Equal(18f, style.PointSize);
        Assert.True(style.IsItalic);
    }

    [Fact]
    public void ALaterRefinementOverridesAnEarlierOne() =>
        Approximately.Equal(20f, StyleOf(span => span.PointSize(10).PointSize(20)).PointSize);

    [Fact]
    public void RefinementsBuildOnTheInheritedStyle()
    {
        TypeStyle style = StyleOf(span => span.Bold(), inherited => inherited.WithTypeface("Times"));

        Assert.Equal("Times", style.Typeface);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void ARefinementStaysWithItsOwnSpan()
    {
        Block root = LayoutHarness.Build(container => container.Text(text =>
        {
            text.Run("a").Bold();
            text.Run("b");
        }));

        List<TextOperation> texts = LayoutHarness.Draw(root, new Extent(200, 200)).Texts.ToList();

        Assert.Equal(TypeWeight.Bold, texts.Single(text => text.Text == "a").Style.Weight);
        Assert.Equal(TypeWeight.Normal, texts.Single(text => text.Text == "b").Style.Weight);
    }
}
