namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// <see cref="TextComposer.MaxLines"/> against <see cref="FakeTypeMeasurer"/>: characters, the ellipsis included,
/// are 6pt wide and lines 12pt tall at the default size.
/// </summary>
public class LineLimitTests
{
    private const float LineHeight = 12f;

    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock element = new TextBlock();
        compose(new TextComposer(element));
        return element;
    }

    private static RecordedPage Draw(TextBlock element, float width) =>
        LayoutHarness.Draw(element, new Extent(width, 500));

    [Fact]
    public void ShowsNoMoreLinesThanTheLimit()
    {
        // At 40pt each word takes a line of its own: five lines, cut to two.
        TextBlock element = Text(text =>
        {
            text.MaxLines(2);
            text.Run("aaa bbb ccc ddd eee");
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(40, 500));

        Assert.True(plan.IsComplete);
        Approximately.Equal(LineHeight * 2, plan.Size.Height);
        Assert.Equal("aaabbb…", Draw(element, 40).Content);
    }

    [Fact]
    public void TextThatFitsTheLimitIsLeftAlone()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(3);
            text.Run("aaa bbb");
        });

        Assert.Equal("aaabbb", Draw(element, 40).Content);
    }

    [Fact]
    public void TheLastLineIsCutBackCharacterByCharacterToMakeRoom()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(2);
            text.Run("aaaa bbbb cccc");
        });

        // "bbbb" fills the 24pt line, so it loses a character to the ellipsis.
        Assert.Equal("aaaabbb…", Draw(element, 24).Content);
    }

    [Fact]
    public void AWordThatCannotKeepACharacterGoesWithTheSpaceBeforeIt()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1);
            text.Run("aaaa b cc");
        });

        RecordedPage page = Draw(element, 36);

        // The ellipsis follows "aaaa" directly, drawn with it as one piece of text.
        Assert.Equal("aaaa…", Assert.Single(page.Texts).Text);
    }

    [Fact]
    public void ExplicitLineBreaksCountTowardsTheLimit()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(2);
            text.Run("a\nb\nc");
        });

        Assert.Equal("ab…", Draw(element, 100).Content);
    }

    [Fact]
    public void TheEllipsisCanBeAnyText()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1, " [more]");
            text.Run("aaa bbb ccc");
        });

        Assert.Equal("aaa [more]", Draw(element, 60).Content);
    }

    [Fact]
    public void AnEmptyEllipsisCutsWithoutAMark()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1, string.Empty);
            text.Run("aaa bbb ccc");
        });

        RecordedPage page = Draw(element, 40);

        Assert.Equal("aaa", page.Content);
        Assert.Single(page.Texts);
    }

    [Fact]
    public void TheEllipsisIsSetInTheParagraphsType()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1);
            text.DefaultType(style => style.WithInk(TestInks.Blue));
            text.Run("aaa ");
            text.Run("bbb ccc").Ink(TestInks.Red);
        });

        TextOperation ellipsis = Draw(element, 50).Texts.Single(text => text.Text == "…");

        Assert.Equal(TestInks.Blue, ellipsis.Style.Ink);
    }

    [Fact]
    public void ALineCutShortTakesTheHeightOfWhatRemains()
    {
        // "aa B" is 30pt, too wide for the ellipsis beside it in 34pt; the 24pt word is cut away entirely, and the
        // line's height goes with it.
        TextBlock element = Text(text =>
        {
            text.MaxLines(1);
            text.Run("aa ");
            text.Run("B").PointSize(24);
            text.Run(" cc");
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(34, 500));

        Approximately.Equal(LineHeight, plan.Size.Height);
        Assert.Equal("aa…", Draw(element, 34).Content);
    }

    [Fact]
    public void AnInlineFrameAtTheEndOfTheLastLineMakesWayForTheEllipsis()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1);
            text.Run("aa ");
            text.Inline(frame => frame.Width(18).Height(6).Fill(TestInks.Red));
            text.Run(" bbbbbbbbbbbb");
        });

        RecordedPage page = Draw(element, 36);

        // "aa", a space and the 18pt frame fill the 36pt line, leaving no room for the ellipsis.
        Assert.Equal("aa…", page.Content);
        Assert.DoesNotContain(page.Operations, operation => operation is RectangleOperation);
    }

    [Fact]
    public void AnInlineFrameBeyondTheLimitIsNeverPlanned()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1);
            text.Run("aaa bbb");
        });
        element.Runs.Add(new Text.TextRun { Inline = new NeverFinishingBlock() });

        Fit plan = LayoutHarness.Measure(element, new Extent(24, 500));

        Assert.True(plan.IsComplete);
        Assert.Equal("aaa…", Draw(element, 24).Content);
    }

    [Fact]
    public void AnEllipsisWiderThanTheLineIsStillSet()
    {
        TextBlock element = Text(text =>
        {
            text.MaxLines(1, "(continued)");
            text.Run("aaa bbb");
        });

        Assert.Equal("(continued)", Draw(element, 30).Content);
    }

    [Fact]
    public void TheLimitMustBeAtLeastOneLine()
    {
        TextComposer text = new TextComposer(new TextBlock());

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => text.MaxLines(0));

        Assert.Equal("count", exception.ParamName);
    }

    [Fact]
    public void TheEllipsisCannotBeNull()
    {
        TextComposer text = new TextComposer(new TextBlock());

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => text.MaxLines(1, null!));

        Assert.Equal("ellipsis", exception.ParamName);
    }
}
