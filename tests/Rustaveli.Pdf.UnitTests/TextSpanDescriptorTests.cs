namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Span refinements, observed as the style the canvas is actually asked to draw with.
/// </summary>
public class TextSpanDescriptorTests
{
    /// <summary>
    /// Draws a one-span paragraph and returns the style of the single run it produced.
    /// </summary>
    /// <param name="refine">The refinements under test.</param>
    /// <param name="inherited">The surrounding default, so a refinement can be seen overriding it.</param>
    private static TextStyle StyleOf(
        Func<TextSpanDescriptor, TextSpanDescriptor> refine,
        Func<TextStyle, TextStyle>? inherited = null)
    {
        Element root = LayoutHarness.Build(container => container
            .DefaultTextStyle(inherited ?? (style => style))
            .Text(text => refine(text.Span("x"))));

        return Assert.Single(LayoutHarness.Draw(root, new Size(200, 200)).Texts).Style;
    }

    [Fact]
    public void FontFamilySetsTheTypeface() =>
        Assert.Equal("Courier", StyleOf(span => span.FontFamily("Courier")).FontFamily);

    [Fact]
    public void FontSizeSetsTheSize() =>
        Approximately.Equal(20f, StyleOf(span => span.FontSize(20)).FontSize);

    [Fact]
    public void FontColorSetsTheColour() =>
        Assert.Equal((Ink)TestInks.Red, StyleOf(span => span.FontColor(TestInks.Red)).Color);

    [Fact]
    public void FontColorAcceptsHex() =>
        Assert.Equal(Ink.Rgb(0x33, 0x66, 0x99), StyleOf(span => span.FontColor("#336699")).Color);

    [Fact]
    public void BackgroundColorSetsTheHighlight() =>
        Assert.Equal((Ink)TestInks.Yellow, StyleOf(span => span.BackgroundColor(TestInks.Yellow)).BackgroundColor);

    [Fact]
    public void BackgroundColorAcceptsHex() =>
        Assert.Equal(Ink.Rgb(0xFF, 0xFF, 0x00), StyleOf(span => span.BackgroundColor("#FFFF00")).BackgroundColor);

    [Fact]
    public void WeightSetsAnyWeight() =>
        Assert.Equal(FontWeight.Light, StyleOf(span => span.Weight(FontWeight.Light)).Weight);

    [Fact]
    public void BoldSetsTheBoldWeight() =>
        Assert.Equal(FontWeight.Bold, StyleOf(span => span.Bold()).Weight);

    [Fact]
    public void ItalicWithoutAnArgumentSwitchesItalicOn() =>
        Assert.True(StyleOf(span => span.Italic()).IsItalic);

    [Fact]
    public void UnderlineWithoutAnArgumentSwitchesUnderlineOn() =>
        Assert.True(StyleOf(span => span.Underline()).HasUnderline);

    [Fact]
    public void StrikethroughWithoutAnArgumentSwitchesStrikethroughOn() =>
        Assert.True(StyleOf(span => span.Strikethrough()).HasStrikethrough);

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
            StyleOf(span => span.Strikethrough(value), style => style.Strikethrough(!value)).HasStrikethrough);

    [Fact]
    public void LineHeightSetsTheMultiplier() =>
        Approximately.Equal(1.5f, StyleOf(span => span.LineHeight(1.5f)).LineHeight);

    [Fact]
    public void LetterSpacingSetsTheGap() =>
        Approximately.Equal(2f, StyleOf(span => span.LetterSpacing(2)).LetterSpacing);

    [Fact]
    public void SubscriptLowersTheRun() =>
        Assert.Equal(FontPosition.Subscript, StyleOf(span => span.Subscript()).Position);

    [Fact]
    public void SuperscriptRaisesTheRun() =>
        Assert.Equal(FontPosition.Superscript, StyleOf(span => span.Superscript()).Position);

    [Fact]
    public void StyleAppliesAnArbitraryTransformation()
    {
        TextStyle style = StyleOf(span => span.Style(inherited => inherited.FontSizeOf(30).Bold()));

        Approximately.Equal(30f, style.FontSize);
        Assert.Equal(FontWeight.Bold, style.Weight);
    }

    [Fact]
    public void ChainedRefinementsAllApply()
    {
        TextStyle style = StyleOf(span => span.Bold().FontSize(18).Italic());

        Assert.Equal(FontWeight.Bold, style.Weight);
        Approximately.Equal(18f, style.FontSize);
        Assert.True(style.IsItalic);
    }

    [Fact]
    public void ALaterRefinementOverridesAnEarlierOne() =>
        Approximately.Equal(20f, StyleOf(span => span.FontSize(10).FontSize(20)).FontSize);

    [Fact]
    public void RefinementsBuildOnTheInheritedStyle()
    {
        TextStyle style = StyleOf(span => span.Bold(), inherited => inherited.FontFamilyOf("Times"));

        Assert.Equal("Times", style.FontFamily);
        Assert.Equal(FontWeight.Bold, style.Weight);
    }

    [Fact]
    public void ARefinementStaysWithItsOwnSpan()
    {
        Element root = LayoutHarness.Build(container => container.Text(text =>
        {
            text.Span("a").Bold();
            text.Span("b");
        }));

        List<TextOperation> texts = LayoutHarness.Draw(root, new Size(200, 200)).Texts.ToList();

        Assert.Equal(FontWeight.Bold, texts.Single(text => text.Text == "a").Style.Weight);
        Assert.Equal(FontWeight.Normal, texts.Single(text => text.Text == "b").Style.Weight);
    }
}
