namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Regression cover for first-line indent and paragraph spacing.
/// </summary>
/// <remarks>
/// Against <see cref="FakeTypeMeasurer"/>: characters are 6pt wide and lines 12pt tall at the default size.
/// </remarks>
public class ParagraphIndentTests
{
    private const float LineHeight = 12f;

    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock block = new TextBlock();
        compose(new TextComposer(block));
        return block;
    }

    [Fact]
    public void ReportedWidthIncludesTheIndentItWillDraw()
    {
        // The parent sizes boxes from this number. Reporting ink-only width while drawing at an offset makes
        // the content overflow whatever box the parent derived.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Run("Hello");
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        // Five characters of ink at 6pt, pushed right by the 20pt indent.
        Approximately.Equal(50f, plan.Size.Width);
    }

    [Fact]
    public void AutoSizedParentDoesNotShatterIndentedText()
    {
        // An Auto row item derives its width from the measured width. If that width excluded the indent, the
        // text would be re-wrapped into a box narrower than it needs and collapse to one character per line.
        ColumnsBlock row = new ColumnsBlock();
        ColumnSlot item = new ColumnSlot { Sizing = ColumnSizing.Natural };

        TextBlock text = Text(paragraph =>
        {
            paragraph.FirstLineIndent(20);
            paragraph.Run("Hello");
        });

        item.Child = text;
        row.Items.Add(item);

        RecordedPage page = LayoutHarness.Render(row, new Extent(500, 500));

        Assert.Equal("Hello", page.Content);
    }

    [Fact]
    public void CentredTextIsNotNarrowedByAnIndentItNeverDraws()
    {
        // The indent is only drawn for left-aligned text, so it must not be deducted from the wrap budget for
        // any other alignment — otherwise it silently costs a line's worth of room and shows nothing for it.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Centered();
            text.Run("aaaaaaa");
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(48, 500));

        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void RightAlignedTextIsNotNarrowedEither()
    {
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.FlushRight();
            text.Run("aaaaaaa");
        });

        Approximately.Equal(LineHeight, LayoutHarness.Plan(block, new Extent(48, 500)).Size.Height);
    }

    [Fact]
    public void RightToLeftTextIsIndentedFromTheRightAndNarrowedAlike()
    {
        // Right-to-left text starts from the right, so that is where its indent goes, and the opening line loses
        // the same 20pt: seven 6pt characters no longer fit in the 28pt left of a 48pt box.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Run("aaaaaaa");
        });

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        Approximately.Equal(LineHeight * 2, LayoutHarness.Plan(block, new Extent(48, 500), context).Size.Height);
    }

    [Fact]
    public void FlushLeftTextIsNotIndentedWhenItReadsRightToLeft()
    {
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.FlushLeft();
            text.Run("aaaaaaa");
        });

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        Approximately.Equal(LineHeight, LayoutHarness.Plan(block, new Extent(48, 500), context).Size.Height);
    }

    [Fact]
    public void AnIndentWiderThanTheBoxWrapsRatherThanStackingCharacters()
    {
        // The zero-width guard exists to stop exactly this. An indent that consumes the whole box must trip it.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(100);
            text.Run("hello world");
        });

        Assert.True(LayoutHarness.Plan(block, new Extent(100, 5000)).IsDeferred);
    }

    [Fact]
    public void ContinuationLinesUseTheFullWidth()
    {
        // Only a paragraph's opening line is indented, so a word that fits the full width must not be broken
        // just because the opening line was narrower.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Run("aa bbbbbbb");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(42, 900));

        Assert.Equal("aabbbbbbb", page.Content);
    }

    [Fact]
    public void NonBreakingSpacesSurviveTrailingTrim()
    {
        // The tokeniser treats a non-breaking space as ink, so the trailing-whitespace trim must agree.
        TextBlock block = Text(text => text.Run("   "));

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        Approximately.Equal(18f, plan.Size.Width);
    }

    [Fact]
    public void ALinkOnAWhitespaceRunIsNotDiscarded()
    {
        TextBlock block = Text(text =>
        {
            text.Run("Fig.");
            text.Link(" ", "https://example.com");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));

        Assert.Single(page.Operations.OfType<ExternalLinkOperation>());
    }

    [Fact]
    public void ABlankLineDoesNotEarnParagraphSpacing()
    {
        TextBlock block = Text(text =>
        {
            text.SpaceBetweenParagraphs(8);
            text.Line("A");
            text.BlankLine();
            text.Line("B");
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        // Three lines, and a single gap before the paragraph that follows the blank one.
        Approximately.Equal(3 * LineHeight + 8, plan.Size.Height);
    }

    [Fact]
    public void ABareCarriageReturnBreaksTheLine()
    {
        TextBlock block = Text(text => text.Run("a\rb"));

        Approximately.Equal(2 * LineHeight, LayoutHarness.Plan(block, new Extent(500, 500)).Size.Height);
    }

    [Fact]
    public void CarriageReturnAndNewlineTogetherBreakOnlyOnce()
    {
        TextBlock block = Text(text => text.Run("a\r\nb"));

        Approximately.Equal(2 * LineHeight, LayoutHarness.Plan(block, new Extent(500, 500)).Size.Height);
    }
}
