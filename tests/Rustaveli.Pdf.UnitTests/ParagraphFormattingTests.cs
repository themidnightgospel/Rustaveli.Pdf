namespace Rustaveli.Pdf.UnitTests;

public class ParagraphFormattingTests
{
    private const float LineHeight = 12f;

    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock block = new TextBlock();
        compose(new TextComposer(block));
        return block;
    }

    [Fact]
    public void IndentsTheOpeningLineOfEachParagraph()
    {
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Line("first");
            text.Run("second");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        List<TextOperation> drawn = page.Texts.ToList();

        Approximately.Equal(20f, drawn[0].Position.X);
        Approximately.Equal(20f, drawn[1].Position.X);
    }

    [Fact]
    public void DoesNotIndentWrappedContinuationLines()
    {
        // Only the opening line of a paragraph is indented; lines produced by wrapping are not.
        TextBlock block = Text(text =>
        {
            text.FirstLineIndent(20);
            text.Run("aaa bbb");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(24, 500));
        List<TextOperation> drawn = page.Texts.ToList();

        Approximately.Equal(20f, drawn[0].Position.X);
        Approximately.Equal(0f, drawn[1].Position.X);
    }

    [Fact]
    public void AddsSpacingBetweenParagraphsButNotBeforeTheFirst()
    {
        TextBlock block = Text(text =>
        {
            text.SpaceBetweenParagraphs(8);
            text.Line("first");
            text.Run("second");
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        // Two lines plus a single gap between them.
        Approximately.Equal(2 * LineHeight + 8, plan.Size.Height);
    }

    [Fact]
    public void WrappedLinesDoNotEarnParagraphSpacing()
    {
        TextBlock block = Text(text =>
        {
            text.SpaceBetweenParagraphs(8);
            text.Run("aaa bbb");
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(24, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }
}
