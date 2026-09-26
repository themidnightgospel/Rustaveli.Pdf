namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Text layout against <see cref="FakeTypeMeasurer"/>: every character is half the font size wide, and a line
/// is exactly the font size tall. At the default size of 12 that makes characters 6pt wide and lines 12pt tall.
/// </summary>
public class TextBlockTests
{
    private const float CharacterWidth = 6f;
    private const float LineHeight = 12f;

    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock element = new TextBlock();
        compose(new TextComposer(element));
        return element;
    }

    [Fact]
    public void MeasuresASingleLineFromCharacterCount()
    {
        TextBlock element = Text(text => text.Run("Hello"));

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        Approximately.Equal(5 * CharacterWidth, plan.Size.Width);
        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void WrapsAtTheAvailableWidth()
    {
        // Six characters fit in 36pt, so the two words land on separate lines.
        TextBlock element = Text(text => text.Run("aaaaaa bbbbbb"));

        Fit plan = LayoutHarness.Measure(element, new Extent(36, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void BreaksOnAnExplicitNewline()
    {
        TextBlock element = Text(text => text.Run("a\nb"));

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void LineAppendsABreakAfterTheText()
    {
        TextBlock element = Text(text =>
        {
            text.Line("first");
            text.Run("second");
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void DropsTheSpaceAtAWrapPoint()
    {
        TextBlock element = Text(text => text.Run("aaa bbb"));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(18, 500));
        List<string> drawn = page.Texts.Select(operation => operation.Text).ToList();

        Assert.DoesNotContain(drawn, text => text.Trim().Length == 0);
    }

    [Fact]
    public void SplitsAWordTooLongForAnyLine()
    {
        // Twelve characters need 72pt but only 18pt (three characters) is available per line.
        TextBlock element = Text(text => text.Run("aaaaaaaaaaaa"));

        Fit plan = LayoutHarness.Measure(element, new Extent(18, 500));

        Approximately.Equal(4 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void StopsAtTheAvailableHeightAndReportsPartial()
    {
        TextBlock element = Text(text => text.Run("aaa bbb ccc ddd"));

        // Room for two of the four lines.
        Fit plan = LayoutHarness.Measure(element, new Extent(18, 2 * LineHeight));

        Assert.True(plan.IsPartial);
        Approximately.Equal(2 * LineHeight, plan.Size.Height);
    }

    [Fact]
    public void ContinuesFromTheLineItStoppedAt()
    {
        TextBlock element = Text(text => text.Run("aaa bbb ccc"));
        Extent space = new Extent(18, LineHeight);

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
        TextBlock element = Text(text => text.Run("aaa bbb ccc"));

        RecordedPage first = LayoutHarness.Draw(element, new Extent(18, LineHeight));
        RecordedPage second = LayoutHarness.Draw(element, new Extent(42, LineHeight));
        RecordedPage third = LayoutHarness.Draw(element, new Extent(42, LineHeight));

        Assert.Equal("aaa", first.Content);
        Assert.Equal("bbb", second.Content);
        Assert.Equal("ccc", third.Content);
    }

    [Fact]
    public void WrapsRatherThanStackingSingleCharactersWhenThereIsNoWidth()
    {
        // A zero-width box cannot hold text. Reporting a successful render of one character per line would
        // produce thousands of pages instead of surfacing the layout mistake.
        TextBlock element = Text(text => text.Run("hello world"));

        Fit plan = LayoutHarness.Measure(element, new Extent(0, 500));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void TrailingSpaceDoesNotCountTowardsLineWidth()
    {
        // A space landing at the end of a line is not ink. Counting it shifts centred text and overstates the
        // width that Auto columns and table cells are sized from.
        TextBlock element = Text(text => text.Run("aaa bbb ccc"));

        // 60pt fits "aaa bbb " (48pt including the trailing space) but not "ccc".
        Fit plan = LayoutHarness.Measure(element, new Extent(60, 500));

        Approximately.Equal(42f, plan.Size.Width);
    }

    [Fact]
    public void DoesNotBreakAtANonBreakingSpace()
    {
        // U+00A0 exists to hold "10 000" together; treating it as a break opportunity defeats its only purpose.
        // Both strings are too wide for the box, so the discriminator is *where* the first line ends.
        TextBlock breakable = Text(text => text.Run("AAA BBB"));
        TextBlock nonBreaking = Text(text => text.Run("AAA\u00A0BBB"));

        Extent space = new Extent(24, LineHeight);

        Assert.Equal("AAA", LayoutHarness.Draw(breakable, space).Content);
        Assert.NotEqual("AAA", LayoutHarness.Draw(nonBreaking, space).Content);
    }

    [Fact]
    public void BlankLinesTakeTheInheritedSize()
    {
        TextBlock element = Text(text =>
        {
            text.DefaultType(style => style.WithPointSize(40));
            text.Line("A");
            text.BlankLine();
            text.Line("B");
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        // Three lines of 40pt, not two of 40 and one of the library default.
        Approximately.Equal(120f, plan.Size.Height);
    }

    [Fact]
    public void ReportsEmptyOnceEveryLineIsDrawn()
    {
        TextBlock element = Text(text => text.Run("aaa"));
        Extent space = new Extent(500, 500);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsNothing);
    }

    [Fact]
    public void WrapsWhenNotEvenOneLineFits()
    {
        TextBlock element = Text(text => text.Run("aaa"));

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 5));

        Assert.True(plan.IsDeferred);
    }

    [Theory]
    [InlineData(nameof(HorizontalPlacement.Left), 0f)]
    [InlineData(nameof(HorizontalPlacement.Center), 35f)]
    [InlineData(nameof(HorizontalPlacement.Right), 70f)]
    public void AlignsLinesWithinTheAvailableWidth(string placement, float expectedX)
    {
        TextBlock element = Text(text => text.Run("Hello"));
        element.Alignment = (HorizontalPlacement)Enum.Parse(typeof(HorizontalPlacement), placement);

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100));
        TextOperation operation = Assert.Single(page.Texts);

        Approximately.Equal(expectedX, operation.Position.X);
    }

    [Fact]
    public void PlacesTextOnTheBaseline()
    {
        TextBlock element = Text(text => text.Run("Hello"));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));
        TextOperation operation = Assert.Single(page.Texts);

        // Ascent is 80% of the 12pt font size.
        Approximately.Equal(9.6f, operation.Position.Y);
    }

    [Fact]
    public void SpansInheritTheContextStyle()
    {
        TextBlock element = Text(text => text.Run("Hello"));
        PlanContext context = LayoutHarness.Context(defaultStyle: TypeStyle.Default.WithPointSize(20));

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500), context);

        Approximately.Equal(5 * 10f, plan.Size.Width);
    }

    [Fact]
    public void SpanStyleRefinesRatherThanReplacesTheInheritedStyle()
    {
        TextBlock element = Text(text => text.Run("Hello").Bold());
        PlanContext context = LayoutHarness.Context(defaultStyle: TypeStyle.Default.WithPointSize(20));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500), context);
        TextOperation operation = Assert.Single(page.Texts);

        Assert.Equal(TypeWeight.Bold, operation.Style.Weight);
        Approximately.Equal(20f, operation.Style.PointSize);
    }

    [Fact]
    public void BlockDefaultAppliesBeneathTheInheritedStyle()
    {
        TextBlock element = Text(text =>
        {
            text.DefaultType(style => style.WithPointSize(24));
            text.Run("Hi");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));
        TextOperation operation = Assert.Single(page.Texts);

        Approximately.Equal(24f, operation.Style.PointSize);
    }

    [Fact]
    public void TallestRunSetsTheLineSpacing()
    {
        TextBlock element = Text(text =>
        {
            text.Run("small");
            text.Run("BIG").PointSize(36);
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        Approximately.Equal(36f, plan.Size.Height);
    }

    [Fact]
    public void ResolvesTheCurrentPageNumber()
    {
        TextBlock element = Text(text => text.Folio());
        Pagination page = new Pagination { Folio = 7 };

        RecordedPage recorded = LayoutHarness.Draw(element, new Extent(500, 500), LayoutHarness.Context(page));

        Assert.Equal("7", recorded.Content);
    }

    [Fact]
    public void DrawsAnUnderlineBeneathTheRun()
    {
        TextBlock element = Text(text => text.Run("Hello").Underline());

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));

        Assert.Single(page.Operations.OfType<LineOperation>());
    }

    [Fact]
    public void PaintsTheHighlightBehindTheRun()
    {
        TextBlock element = Text(text => text.Run("Hello").Highlight(TestInks.Yellow));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));
        RectangleOperation highlight = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Yellow, highlight.Ink);
    }

    [Fact]
    public void MarksLinkedRunsAsClickable()
    {
        TextBlock element = Text(text => text.Link("click", "https://example.com"));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));
        ExternalLinkOperation link = Assert.Single(page.Operations.OfType<ExternalLinkOperation>());

        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void ShrinksSubscriptRunsBelowTheirNominalSize()
    {
        TextBlock normal = Text(text => text.Run("H2O"));
        TextBlock withSubscript = Text(text =>
        {
            text.Run("H");
            text.Run("2").Subscript();
            text.Run("O");
        });

        float normalWidth = LayoutHarness.Measure(normal, new Extent(500, 500)).Size.Width;
        float subscriptWidth = LayoutHarness.Measure(withSubscript, new Extent(500, 500)).Size.Width;

        Assert.True(subscriptWidth < normalWidth, "A subscript digit should be narrower than a full-size one.");
    }

    [Fact]
    public void DrawsNothingOnceEveryLineIsDrawn()
    {
        TextBlock element = Text(text => text.Run("aaa"));
        Extent space = new Extent(500, 500);

        LayoutHarness.Draw(element, space);

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void AnAttemptWithoutRoomForALineLeavesTheWrappingOpen()
    {
        // Nothing was drawn, so nothing may be pinned: a later, wider box must be free to wrap afresh rather than
        // inherit the two-line wrapping of the narrow attempt.
        TextBlock element = Text(text => text.Run("aaa bbb"));

        RecordedPage cramped = LayoutHarness.Draw(element, new Extent(18, 5));
        RecordedPage roomy = LayoutHarness.Draw(element, new Extent(500, 500));

        Assert.Empty(cramped.Operations);
        Assert.Equal("aaa bbb", roomy.Content);
        Assert.All(roomy.Texts, operation => Approximately.Equal(9.6f, operation.Position.Y));
    }

    [Fact]
    public void PlacesTheUnderlineHalfTheDescentBelowTheBaseline()
    {
        TextBlock element = Text(text => text.Run("Hello").Underline());

        LineOperation line = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 500)).Operations.OfType<LineOperation>());

        // Baseline 9.6 plus half the 2.4pt descent; five 6pt characters long.
        Approximately.Equal(new Offset(0, 10.8f), line.Position);
        Approximately.Equal(new Offset(30, 10.8f), line.End);
        Approximately.Equal(0.75f, line.Thickness);
        Assert.Equal(TestInks.Black, line.Ink);
    }

    [Fact]
    public void DrawsAStrikethroughAcrossTheRun()
    {
        TextBlock element = Text(text => text.Run("Hello").StrikeThrough().Ink(TestInks.Red));

        LineOperation line = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 500)).Operations.OfType<LineOperation>());

        // Baseline 9.6 less 30% of the 9.6pt ascent.
        Approximately.Equal(new Offset(0, 6.72f), line.Position);
        Approximately.Equal(new Offset(30, 6.72f), line.End);
        Approximately.Equal(0.75f, line.Thickness);
        Assert.Equal(TestInks.Red, line.Ink);
    }

    [Fact]
    public void DecorationLinesAreNeverThinnerThanHalfAPoint()
    {
        TextBlock element = Text(text => text.Run("tiny").PointSize(4).Underline().StrikeThrough());

        List<LineOperation> lines = LayoutHarness.Draw(element, new Extent(500, 500)).Operations.OfType<LineOperation>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Approximately.Equal(0.5f, line.Thickness));
    }

    [Fact]
    public void ALinkCoversOnlyItsOwnRun()
    {
        TextBlock element = Text(text =>
        {
            text.Run("go ");
            text.Link("here", "https://example.com");
        });

        ExternalLinkOperation link = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 500)).Operations.OfType<ExternalLinkOperation>());

        // After three 6pt characters, four characters wide and one 12pt line tall.
        Assert.Equal(new Bounds(18, 0, 42, 12), link.Bounds);
    }

    [Fact]
    public void ACrossReferenceCoversOnlyItsOwnRun()
    {
        TextBlock element = Text(text =>
        {
            text.Run("go ");
            text.CrossReference("here", "intro");
        });

        InternalLinkOperation link = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 500)).Operations.OfType<InternalLinkOperation>());

        Assert.Equal("intro", link.Destination);
        Assert.Equal(new Bounds(18, 0, 42, 12), link.Bounds);
    }

    [Fact]
    public void AParagraphOfOnlyEmptySpansStillOccupiesALine()
    {
        TextBlock element = new TextBlock { DefaultTypeRefinement = style => style.WithPointSize(40) };
        element.Runs.Add(new Text.TextRun { Text = string.Empty });

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        // As tall as a line of the paragraph's own text would be, but with nothing on it.
        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(0, 40), plan.Size);
    }
}
