namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Regression cover for first-line indent and paragraph spacing.
/// </summary>
/// <remarks>
/// Against <see cref="FakeTextMeasurer"/>: characters are 6pt wide and lines 12pt tall at the default size.
/// </remarks>
public class ParagraphIndentTests
{
    private const float LineHeight = 12f;

    private static TextElement Text(Action<TextDescriptor> compose)
    {
        TextElement element = new TextElement();
        compose(new TextDescriptor(element));
        return element;
    }

    [Fact]
    public void ReportedWidthIncludesTheIndentItWillDraw()
    {
        // The parent sizes boxes from this number. Reporting ink-only width while drawing at an offset makes
        // the content overflow whatever box the parent derived.
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Span("Hello");
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        // Five characters of ink at 6pt, pushed right by the 20pt indent.
        Approximately.Equal(50f, plan.Size.Width);
    }

    [Fact]
    public void AutoSizedParentDoesNotShatterIndentedText()
    {
        // An Auto row item derives its width from the measured width. If that width excluded the indent, the
        // text would be re-wrapped into a box narrower than it needs and collapse to one character per line.
        RowElement row = new RowElement();
        RowItem item = new RowItem { Sizing = RowItemSizing.Auto };

        TextElement text = Text(descriptor =>
        {
            descriptor.FirstLineIndent(20);
            descriptor.Span("Hello");
        });

        item.Child = text;
        row.Items.Add(item);

        RecordedPage page = LayoutHarness.Draw(row, new Size(500, 500));

        Assert.Equal("Hello", page.Content);
    }

    [Fact]
    public void CentredTextIsNotNarrowedByAnIndentItNeverDraws()
    {
        // The indent is only drawn for left-aligned text, so it must not be deducted from the wrap budget for
        // any other alignment — otherwise it silently costs a line's worth of room and shows nothing for it.
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(20);
            text.AlignCenter();
            text.Span("aaaaaaa");
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(48, 500));

        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void RightAlignedTextIsNotNarrowedEither()
    {
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(20);
            text.AlignRight();
            text.Span("aaaaaaa");
        });

        Approximately.Equal(LineHeight, LayoutHarness.Measure(element, new Size(48, 500)).Size.Height);
    }

    [Fact]
    public void RightToLeftTextIsNotNarrowedEither()
    {
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Span("aaaaaaa");
        });

        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        Approximately.Equal(LineHeight, LayoutHarness.Measure(element, new Size(48, 500), context).Size.Height);
    }

    [Fact]
    public void AnIndentWiderThanTheBoxWrapsRatherThanStackingCharacters()
    {
        // The zero-width guard exists to stop exactly this. An indent that consumes the whole box must trip it.
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(100);
            text.Span("hello world");
        });

        Assert.True(LayoutHarness.Measure(element, new Size(100, 5000)).IsWrap);
    }

    [Fact]
    public void ContinuationLinesUseTheFullWidth()
    {
        // Only a paragraph's opening line is indented, so a word that fits the full width must not be broken
        // just because the opening line was narrower.
        TextElement element = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Span("aa bbbbbbb");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(42, 900));

        Assert.Equal("aabbbbbbb", page.Content);
    }

    [Fact]
    public void NonBreakingSpacesSurviveTrailingTrim()
    {
        // The tokeniser treats a non-breaking space as ink, so the trailing-whitespace trim must agree.
        TextElement element = Text(text => text.Span("   "));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(18f, plan.Size.Width);
    }

    [Fact]
    public void ALinkOnAWhitespaceRunIsNotDiscarded()
    {
        TextElement element = Text(text =>
        {
            text.Span("Fig.");
            text.Hyperlink(" ", "https://example.com");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));

        Assert.Single(page.Operations.OfType<ExternalLinkOperation>());
    }

    [Fact]
    public void ABlankLineDoesNotEarnParagraphSpacing()
    {
        TextElement element = Text(text =>
        {
            text.ParagraphSpacing(8);
            text.Line("A");
            text.EmptyLine();
            text.Line("B");
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        // Three lines, and a single gap before the paragraph that follows the blank one.
        Approximately.Equal(3 * LineHeight + 8, plan.Size.Height);
    }

    [Fact]
    public void ABareCarriageReturnBreaksTheLine()
    {
        TextElement element = Text(text => text.Span("a\rb"));

        Approximately.Equal(2 * LineHeight, LayoutHarness.Measure(element, new Size(500, 500)).Size.Height);
    }

    [Fact]
    public void CarriageReturnAndNewlineTogetherBreakOnlyOnce()
    {
        TextElement element = Text(text => text.Span("a\r\nb"));

        Approximately.Equal(2 * LineHeight, LayoutHarness.Measure(element, new Size(500, 500)).Size.Height);
    }
}
