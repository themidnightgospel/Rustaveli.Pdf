namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Text layout against <see cref="FakeTextMeasurer"/>: every character is half the font size wide, and a line
/// is exactly the font size tall. At the default size of 12 that makes characters 6pt wide and lines 12pt tall.
/// </summary>
public class TextElementTests
{
    private const float CharacterWidth = 6f;
    private const float LineHeight = 12f;

    private static TextElement Text(Action<TextDescriptor> compose)
    {
        TextElement element = new TextElement();
        compose(new TextDescriptor(element));
        return element;
    }

    [Fact]
    public void MeasuresASingleLineFromCharacterCount()
    {
        TextElement element = Text(text => text.Span("Hello"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(5 * CharacterWidth, plan.Size.Width);
        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void WrapsAtTheAvailableWidth()
    {
        // Six characters fit in 36pt, so the two words land on separate lines.
        TextElement element = Text(text => text.Span("aaaaaa bbbbbb"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(36, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void BreaksOnAnExplicitNewline()
    {
        TextElement element = Text(text => text.Span("a\nb"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void LineAppendsABreakAfterTheText()
    {
        TextElement element = Text(text =>
        {
            text.Line("first");
            text.Span("second");
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void DropsTheSpaceAtAWrapPoint()
    {
        TextElement element = Text(text => text.Span("aaa bbb"));

        RecordedPage page = LayoutHarness.Draw(element, new Size(18, 500));
        List<string> drawn = page.Texts.Select(operation => operation.Text).ToList();

        Assert.DoesNotContain(drawn, text => text.Trim().Length == 0);
    }

    [Fact]
    public void SplitsAWordTooLongForAnyLine()
    {
        // Twelve characters need 72pt but only 18pt (three characters) is available per line.
        TextElement element = Text(text => text.Span("aaaaaaaaaaaa"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(18, 500));

        Approximately.Equal(4 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void StopsAtTheAvailableHeightAndReportsPartialRender()
    {
        TextElement element = Text(text => text.Span("aaa bbb ccc ddd"));

        // Room for two of the four lines.
        SpacePlan plan = LayoutHarness.Measure(element, new Size(18, 2 * LineHeight));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void ContinuesFromTheLineItStoppedAt()
    {
        TextElement element = Text(text => text.Span("aaa bbb ccc"));
        Size space = new Size(18, LineHeight);

        RecordedPage first = LayoutHarness.Draw(element, space);
        RecordedPage second = LayoutHarness.Draw(element, space);

        Assert.Equal("aaa", first.Content);
        Assert.Equal("bbb", second.Content);
    }

    [Fact]
    public void KeepsItsWrappingWhenTheOfferedWidthChangesMidFlow()
    {
        // The line cursor indexes one particular wrapping. Re-wrapping at a new width after content has already
        // been drawn would make it point somewhere else entirely, silently losing or repeating lines.
        TextElement element = Text(text => text.Span("aaa bbb ccc"));

        RecordedPage first = LayoutHarness.Draw(element, new Size(18, LineHeight));
        RecordedPage second = LayoutHarness.Draw(element, new Size(42, LineHeight));
        RecordedPage third = LayoutHarness.Draw(element, new Size(42, LineHeight));

        Assert.Equal("aaa", first.Content);
        Assert.Equal("bbb", second.Content);
        Assert.Equal("ccc", third.Content);
    }

    [Fact]
    public void WrapsRatherThanStackingSingleCharactersWhenThereIsNoWidth()
    {
        // A zero-width box cannot hold text. Reporting a successful render of one character per line would
        // produce thousands of pages instead of surfacing the layout mistake.
        TextElement element = Text(text => text.Span("hello world"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(0, 500));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void TrailingSpaceDoesNotCountTowardsLineWidth()
    {
        // A space landing at the end of a line is not ink. Counting it shifts centred text and overstates the
        // width that Auto columns and table cells are sized from.
        TextElement element = Text(text => text.Span("aaa bbb ccc"));

        // 60pt fits "aaa bbb " (48pt including the trailing space) but not "ccc".
        SpacePlan plan = LayoutHarness.Measure(element, new Size(60, 500));

        Approximately.Equal(42f, plan.Size.Width);
    }

    [Fact]
    public void DoesNotBreakAtANonBreakingSpace()
    {
        // U+00A0 exists to hold "10 000" together; treating it as a break opportunity defeats its only purpose.
        // Both strings are too wide for the box, so the discriminator is *where* the first line ends.
        TextElement breakable = Text(text => text.Span("AAA BBB"));
        TextElement nonBreaking = Text(text => text.Span("AAA\u00A0BBB"));

        Size space = new Size(24, LineHeight);

        Assert.Equal("AAA", LayoutHarness.Draw(breakable, space).Content);
        Assert.NotEqual("AAA", LayoutHarness.Draw(nonBreaking, space).Content);
    }

    [Fact]
    public void BlankLinesTakeTheInheritedSize()
    {
        TextElement element = Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSizeOf(40));
            text.Line("A");
            text.EmptyLine();
            text.Line("B");
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        // Three lines of 40pt, not two of 40 and one of the library default.
        Approximately.Equal(120f, plan.Size.Height);
    }

    [Fact]
    public void ReportsEmptyOnceEveryLineIsDrawn()
    {
        TextElement element = Text(text => text.Span("aaa"));
        Size space = new Size(500, 500);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
    }

    [Fact]
    public void WrapsWhenNotEvenOneLineFits()
    {
        TextElement element = Text(text => text.Span("aaa"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 5));

        Assert.True(plan.IsWrap);
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left, 0f)]
    [InlineData(HorizontalAlignment.Center, 35f)]
    [InlineData(HorizontalAlignment.Right, 70f)]
    public void AlignsLinesWithinTheAvailableWidth(HorizontalAlignment alignment, float expectedX)
    {
        TextElement element = Text(text => text.Span("Hello"));
        element.Alignment = alignment;

        RecordedPage page = LayoutHarness.Draw(element, new Size(100, 100));
        TextOperation operation = Assert.Single(page.Texts);

        Approximately.Equal(expectedX, operation.Position.X);
    }

    [Fact]
    public void PlacesTextOnTheBaseline()
    {
        TextElement element = Text(text => text.Span("Hello"));

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        TextOperation operation = Assert.Single(page.Texts);

        // Ascent is 80% of the 12pt font size.
        Approximately.Equal(9.6f, operation.Position.Y);
    }

    [Fact]
    public void SpansInheritTheContextStyle()
    {
        TextElement element = Text(text => text.Span("Hello"));
        LayoutContext context = LayoutHarness.Context(defaultStyle: TextStyle.Default.FontSizeOf(20));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500), context);

        Approximately.Equal(5 * 10f, plan.Size.Width);
    }

    [Fact]
    public void SpanStyleRefinesRatherThanReplacesTheInheritedStyle()
    {
        TextElement element = Text(text => text.Span("Hello").Bold());
        LayoutContext context = LayoutHarness.Context(defaultStyle: TextStyle.Default.FontSizeOf(20));

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500), context);
        TextOperation operation = Assert.Single(page.Texts);

        Assert.Equal(FontWeight.Bold, operation.Style.Weight);
        Approximately.Equal(20f, operation.Style.FontSize);
    }

    [Fact]
    public void BlockDefaultAppliesBeneathTheInheritedStyle()
    {
        TextElement element = Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSizeOf(24));
            text.Span("Hi");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        TextOperation operation = Assert.Single(page.Texts);

        Approximately.Equal(24f, operation.Style.FontSize);
    }

    [Fact]
    public void TallestRunSetsTheLineHeight()
    {
        TextElement element = Text(text =>
        {
            text.Span("small");
            text.Span("BIG").FontSize(36);
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(36f, plan.Size.Height);
    }

    [Fact]
    public void ResolvesTheCurrentPageNumber()
    {
        TextElement element = Text(text => text.CurrentPageNumber());
        PageContext page = new PageContext { CurrentPage = 7 };

        RecordedPage recorded = LayoutHarness.Draw(element, new Size(500, 500), LayoutHarness.Context(page));

        Assert.Equal("7", recorded.Content);
    }

    [Fact]
    public void DrawsAnUnderlineBeneathTheRun()
    {
        TextElement element = Text(text => text.Span("Hello").Underline());

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));

        Assert.Single(page.Operations.OfType<LineOperation>());
    }

    [Fact]
    public void PaintsTheHighlightBehindTheRun()
    {
        TextElement element = Text(text => text.Span("Hello").BackgroundColor(Colors.Yellow));

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        RectangleOperation highlight = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(Colors.Yellow, highlight.Color);
    }

    [Fact]
    public void MarksHyperlinkSpansAsClickable()
    {
        TextElement element = Text(text => text.Hyperlink("click", "https://example.com"));

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        ExternalLinkOperation link = Assert.Single(page.Operations.OfType<ExternalLinkOperation>());

        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void ShrinksSubscriptRunsBelowTheirNominalSize()
    {
        TextElement normal = Text(text => text.Span("H2O"));
        TextElement withSubscript = Text(text =>
        {
            text.Span("H");
            text.Span("2").Subscript();
            text.Span("O");
        });

        float normalWidth = LayoutHarness.Measure(normal, new Size(500, 500)).Size.Width;
        float subscriptWidth = LayoutHarness.Measure(withSubscript, new Size(500, 500)).Size.Width;

        Assert.True(subscriptWidth < normalWidth, "A subscript digit should be narrower than a full-size one.");
    }
}
