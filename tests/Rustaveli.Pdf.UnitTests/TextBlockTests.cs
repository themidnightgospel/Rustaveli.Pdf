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

    /// <summary>Draws a paragraph and returns its underlines, strike-throughs and overlines.</summary>
    /// <param name="element">The paragraph.</param>
    /// <param name="fontPlacesStrokes">Whether the font says where its strokes go and how thick they are.</param>
    private static List<LineOperation> Strokes(TextBlock element, bool fontPlacesStrokes)
    {
        PlanContext context = new PlanContext(new FakeTypeMeasurer(fontPlacesStrokes), new Pagination());

        return LayoutHarness.Draw(element, new Extent(500, 500), context).Operations.OfType<LineOperation>().ToList();
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

    [Theory]
    [InlineData(0x000A)]
    [InlineData(0x000B)]
    [InlineData(0x000C)]
    [InlineData(0x000D)]
    [InlineData(0x0085)]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    public void EveryUnicodeLineEndingEndsTheLine(int ending)
    {
        TextBlock element = Text(text => text.Run($"a{(char)ending}b"));

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));

        // The ending itself is not drawn, and "b" starts the next line.
        Assert.Equal("ab", page.Content);
        Approximately.Equal(9.6f + LineHeight, page.Texts.Single(text => text.Text == "b").Position.Y);
    }

    [Fact]
    public void ACarriageReturnAndLineFeedTogetherEndOneLine()
    {
        TextBlock element = Text(text => text.Run("a\r\nb"));

        Approximately.Equal(2 * LineHeight, LayoutHarness.Measure(element, new Extent(500, 500)).Size.Height);
    }

    [Fact]
    public void ALineMayBreakAfterAHyphen()
    {
        // "well-known" is 60pt; at 36pt it breaks after the hyphen rather than inside a word.
        TextBlock element = Text(text => text.Run("well-known"));

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        Assert.Equal(["well-", "known"], texts.Select(text => text.Text));
        Approximately.Equal(texts[0].Position.Y + LineHeight, texts[1].Position.Y);
    }

    [Fact]
    public void ALineMayBreakBetweenIdeographs()
    {
        // Chinese has no spaces: the line takes as many characters as fit and continues on the next.
        TextBlock element = Text(text => text.Run("ab 世界你好"));

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(30, 500)).Texts.ToList();

        Assert.Equal(["ab 世界", "你好"], texts.Select(text => text.Text));
        Approximately.Equal(0f, texts[1].Position.X);
        Approximately.Equal(texts[0].Position.Y + LineHeight, texts[1].Position.Y);
    }

    [Fact]
    public void ALineDoesNotBreakBeforeClosingPunctuation()
    {
        // "(aa)" and "bb" are 24pt and 12pt; at 30pt a break before the bracket would fit, but is not allowed.
        TextBlock element = Text(text => text.Run("bb (aa)"));

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(30, 500)).Texts.ToList();

        Assert.Equal(["bb", "(aa)"], texts.Select(text => text.Text));
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
    public void AWordTooLongForAnyLineStartsOnAFreshOne()
    {
        TextBlock element = Text(text => text.Run("aa bbbbbbbbbb"));

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        // "aa" keeps its line to itself, less the space it no longer needs; the long word begins the next.
        Assert.Equal(["aa", "bbbbbb", "bbbb"], texts.Select(text => text.Text));
        Approximately.Equal(texts[1].Position.Y, texts[0].Position.Y + LineHeight);
    }

    [Fact]
    public void TypeThatBreaksAnywhereFillsTheLineItIsOn()
    {
        TextBlock element = Text(text => text.Run("aa bbbbbbbbbb").BreakAnywhere());

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        // Six characters to a 36pt line: "aa", a space, then as much of the word as fits.
        Assert.Equal(["aa bbb", "bbbbbb", "b"], texts.Select(text => text.Text));
        Approximately.Equal(texts[0].Position.Y + LineHeight, texts[1].Position.Y);
        Approximately.Equal(0f, texts[1].Position.X);
    }

    [Fact]
    public void TypeThatBreaksAnywhereStillPrefersAWordThatFits()
    {
        TextBlock element = Text(text => text.Run("aaa bb").BreakAnywhere());

        Assert.Equal("aaa bb", LayoutHarness.Draw(element, new Extent(36, 500)).Content);
    }

    [Fact]
    public void ALineWithNoRoomLeftSendsTheBrokenWordOnToTheNext()
    {
        // "aaaaa" and a space fill the 36pt line exactly, so not a character of the next word fits beside them.
        TextBlock element = Text(text => text.Run("aaaaa bbbbbbbb").BreakAnywhere());

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        Assert.Equal(["aaaaa", "bbbbbb", "bb"], texts.Select(text => text.Text));
    }

    [Fact]
    public void OnlyTheRunThatBreaksAnywhereDoesSo()
    {
        TextBlock element = Text(text =>
        {
            text.Run("aa ").BreakAnywhere();
            text.Run("bbbbb");
        });

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        Assert.Equal(["aa", "bbbbb"], texts.Select(text => text.Text));
    }

    [Fact]
    public void AnIndentedOpeningLineThatBreaksAnywhereFillsOnlyTheRoomAfterTheIndent()
    {
        TextBlock element = Text(text =>
        {
            text.FirstLineIndent(12);
            text.Run("aaaaaaaaaa").BreakAnywhere();
        });

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(36, 500)).Texts.ToList();

        Assert.Equal(["aaaa", "aaaaaa"], texts.Select(text => text.Text));
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

    [Theory]
    [InlineData("\u00A0")]
    [InlineData("\u202F")]
    [InlineData("\u2007")]
    public void ANoBreakSpaceBeforeALineEndIsPartOfTheWordNotTrailingSpace(string noBreakSpace)
    {
        // 36pt fits "aaa" and both spaces but not "bbb". The ordinary space after the no-break one is trailing and
        // does not count; the no-break space belongs to the word, so the line is four characters wide, not three.
        TextBlock element = Text(text => text.Run("aaa" + noBreakSpace + " bbb"));

        Fit plan = LayoutHarness.Measure(element, new Extent(36, 500));

        Approximately.Equal(4 * CharacterWidth, plan.Size.Width);
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
    [InlineData(nameof(LineAlignment.Left), 0f)]
    [InlineData(nameof(LineAlignment.Center), 35f)]
    [InlineData(nameof(LineAlignment.Right), 70f)]
    [InlineData(nameof(LineAlignment.Start), 0f)]
    [InlineData(nameof(LineAlignment.End), 70f)]
    [InlineData(nameof(LineAlignment.Justified), 0f)]
    public void AlignsLinesWithinTheAvailableWidth(string alignment, float expectedX)
    {
        TextBlock element = Text(text => text.Run("Hello"));
        element.Alignment = Enum.Parse<LineAlignment>(alignment);

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100));
        TextOperation operation = Assert.Single(page.Texts);

        Approximately.Equal(expectedX, operation.Position.X);
    }

    [Theory]
    [InlineData(nameof(LineAlignment.Left), 0f)]
    [InlineData(nameof(LineAlignment.Center), 35f)]
    [InlineData(nameof(LineAlignment.Right), 70f)]
    [InlineData(nameof(LineAlignment.Start), 70f)]
    [InlineData(nameof(LineAlignment.End), 0f)]
    [InlineData(nameof(LineAlignment.Justified), 70f)]
    public void StartAndEndFollowTheReadingDirection(string alignment, float expectedX)
    {
        TextBlock element = Text(text => text.Run("Hello"));
        element.Alignment = Enum.Parse<LineAlignment>(alignment);
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);

        Approximately.Equal(expectedX, Assert.Single(page.Texts).Position.X);
    }

    [Fact]
    public void JustifiedLinesStretchTheirSpacesToFillTheWidth()
    {
        // Two lines of 60pt: "aaa bbb ccc" on the first, "ddd" on the second.
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("aaa bbb ccc ddd");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));
        List<TextOperation> words = page.Texts.Where(text => text.Text.Trim().Length > 0).ToList();

        // 66pt of text and 14pt of slack across two spaces: each space grows from 6pt to 13pt.
        Assert.Equal(["aaa", "bbb", "ccc", "ddd"], words.Select(word => word.Text));
        Approximately.Equal(0f, words[0].Position.X);
        Approximately.Equal(31f, words[1].Position.X);
        Approximately.Equal(62f, words[2].Position.X);
    }

    [Fact]
    public void ALineInOneTypeIsDrawnAsOnePieceOfText()
    {
        TextBlock element = Text(text => text.Run("aaa bbb ccc"));

        TextOperation line = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 100)).Texts);

        Assert.Equal("aaa bbb ccc", line.Text);
    }

    [Fact]
    public void AChangeOfTypeStartsANewPieceWhereTheWordsWereMeasured()
    {
        TextBlock element = Text(text =>
        {
            text.Run("aaa ");
            text.Run("bbb").Bold();
            text.Run(" ccc");
        });

        List<TextOperation> texts = LayoutHarness.Draw(element, new Extent(500, 100)).Texts.ToList();

        Assert.Equal(["aaa ", "bbb", " ccc"], texts.Select(text => text.Text));
        Assert.Equal([0f, 24f, 42f], texts.Select(text => text.Position.X));
    }

    [Fact]
    public void RunsStyledAlikeAreDrawnAsOnePiece()
    {
        // Each run is given a style of its own; alike is judged by what the styles say, not by which object holds it.
        TextBlock element = Text(text =>
        {
            text.Run("aaa ").Bold();
            text.Run("bbb").Bold();
        });

        TextOperation line = Assert.Single(LayoutHarness.Draw(element, new Extent(500, 100)).Texts);

        Assert.Equal("aaa bbb", line.Text);
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\u2003")]
    public void AGapOfOneCharacterOtherThanASpaceIsDrawnAsTyped(string gap)
    {
        // A gap of one space is handed out as one shared string; a gap of any other single character is not a space.
        TextBlock element = Text(text => text.Run("aaa" + gap + "bbb"));

        Assert.Equal("aaa" + gap + "bbb", Assert.Single(LayoutHarness.Draw(element, new Extent(500, 100)).Texts).Text);
    }

    [Fact]
    public void TrackedTypeIsDrawnAWordAtATime()
    {
        // Tracking falls between the characters of what is measured; words measured apart must be drawn apart.
        TextBlock element = Text(text => text.Run("aa bb").Tracking(1));

        Assert.Equal(["aa", " ", "bb"], LayoutHarness.Draw(element, new Extent(500, 100)).Texts.Select(text => text.Text));
    }

    [Fact]
    public void WordsOfOneLinkAreOneLink()
    {
        TextBlock element = Text(text =>
        {
            text.Link("go here", "https://example.com");
            text.Link(" or there", "https://example.org");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 100));
        List<ExternalLinkOperation> links = page.Operations.OfType<ExternalLinkOperation>().ToList();

        Assert.Equal(["go here", " or there"], page.Texts.Select(text => text.Text));
        Assert.Equal(["https://example.com", "https://example.org"], links.Select(link => link.Url));
        Approximately.Equal(42f, links[0].Size.Width);
    }

    [Fact]
    public void TheLastLineOfAJustifiedParagraphIsNotStretched()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("aaa bbb ccc ddd e");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));
        // A stretched line is drawn a word at a time, its spaces widened; "ddd e" is drawn whole, at its own width.
        TextOperation last = page.Texts.Single(text => text.Text == "ddd e");

        Approximately.Equal(0f, last.Position.X);
        Assert.Contains(page.Texts, text => text.Text == "ccc");
    }

    [Fact]
    public void ALineEndedByABreakIsTheLastOfItsParagraphAndIsNotStretched()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("a b\nc d e f g h i j k l m n");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));

        Approximately.Equal(0f, page.Texts.Single(text => text.Text == "a b").Position.X);
        Assert.True(page.Texts.Single(text => text.Text == "i").Position.X > 48f, "The wrapped line should stretch.");
    }

    [Fact]
    public void AJustifiedLineKeepsItsFirstLineIndent()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.FirstLineIndent(12);
            text.Run("aaa bbb ccc ddd");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));
        List<TextOperation> words = page.Texts.Where(text => text.Text.Trim().Length > 0).ToList();

        // The indent leaves 68pt: "aaa bbb ccc" (66pt) with 2pt of slack, a point for each space.
        Approximately.Equal(12f, words[0].Position.X);
        Approximately.Equal(37f, words[1].Position.X);
        Approximately.Equal(62f, words[2].Position.X);
    }

    [Fact]
    public void JustificationLeavesLeadingWhitespaceAsTyped()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("x\n  aa bb cc dd ee ff");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));

        // The two leading spaces keep their 12pt; the slack goes between the words.
        Approximately.Equal(12f, page.Texts.Single(text => text.Text == "aa").Position.X);
    }

    [Fact]
    public void AJustifiedLineWithASingleWordSitsAtTheStart()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("aaaaaaaaaaaaaaaaaaaa");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(80, 100));

        Assert.All(page.Texts, text => Approximately.Equal(0f, text.Position.X));
    }

    [Fact]
    public void AStretchedSpaceCarriesItsUnderlineAcrossTheGap()
    {
        TextBlock element = Text(text =>
        {
            text.Justified();
            text.Run("aaa bbb ccc ddd").Underline();
        });

        List<LineOperation> lines = LayoutHarness.Draw(element, new Extent(80, 100)).Operations.OfType<LineOperation>().ToList();

        // The first line's strokes run unbroken from 0 to the full 80pt.
        Approximately.Equal(0f, lines[0].Position.X);
        Approximately.Equal(80f, lines[4].End.X);
        for (int index = 1; index < 5; index++)
            Approximately.Equal(lines[index - 1].End.X, lines[index].Position.X);
    }

    [Fact]
    public void ARightToLeftParagraphIsIndentedFromTheRight()
    {
        TextBlock element = Text(text =>
        {
            text.FirstLineIndent(10);
            text.Run("Hello");
        });
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);

        Approximately.Equal(60f, Assert.Single(page.Texts).Position.X);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecorationLinesAreNeverThinnerThanHalfAPoint(bool fontPlacesStrokes)
    {
        TextBlock element = Text(text => text.Run("tiny").PointSize(4).Underline().StrikeThrough().Overline());

        List<LineOperation> lines = Strokes(element, fontPlacesStrokes);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Approximately.Equal(0.5f, line.Thickness));
    }

    [Fact]
    public void PlacesTheUnderlineWhereTheFontSays()
    {
        TextBlock element = Text(text => text.Run("Hello").Underline());

        LineOperation line = Assert.Single(Strokes(element, fontPlacesStrokes: true));

        // Baseline 9.6 plus the font's 1.8pt underline offset, as thick as the font draws it.
        Approximately.Equal(new Offset(0, 11.4f), line.Position);
        Approximately.Equal(new Offset(30, 11.4f), line.End);
        Approximately.Equal(0.6f, line.Thickness);
    }

    [Fact]
    public void PlacesTheStrikethroughWhereTheFontSays()
    {
        TextBlock element = Text(text => text.Run("Hello").StrikeThrough());

        LineOperation line = Assert.Single(Strokes(element, fontPlacesStrokes: true));

        // Baseline 9.6 less the font's 3pt strike-through height.
        Approximately.Equal(new Offset(0, 6.6f), line.Position);
        Approximately.Equal(new Offset(30, 6.6f), line.End);
        Approximately.Equal(0.72f, line.Thickness);
    }

    [Theory]
    [InlineData(false, 0.75f)]
    [InlineData(true, 0.6f)]
    public void DrawsTheOverlineJustInsideTheTopOfTheAscent(bool fontPlacesStrokes, float weight)
    {
        TextBlock element = Text(text => text.Run("Hello").Overline());

        LineOperation line = Assert.Single(Strokes(element, fontPlacesStrokes));

        // The 9.6pt ascent reaches the top of the line; the stroke lies within it, as thick as an underline.
        Approximately.Equal(new Offset(0, weight / 2), line.Position);
        Approximately.Equal(new Offset(30, weight / 2), line.End);
        Approximately.Equal(weight, line.Thickness);
    }

    [Fact]
    public void StrokesFollowARaisedBaseline()
    {
        TextBlock element = Text(text => text.Run("x").Superscript().Underline().Overline());

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));
        float baseline = Assert.Single(page.Texts).Position.Y;
        List<LineOperation> lines = page.Operations.OfType<LineOperation>().ToList();
        TypeMetrics metrics = LayoutHarness.Measurer.GetMetrics(TypeStyle.Default.Superscript());

        Approximately.Equal(baseline + (metrics.Descent / 2), lines[0].Position.Y);
        Approximately.Equal(baseline - metrics.Ascent + (lines[1].Thickness / 2), lines[1].Position.Y);
    }

    [Fact]
    public void AStrokeInkColoursTheStrokesButNotTheType()
    {
        TextBlock element = Text(text => text.Run("Hello").Ink(TestInks.Red).StrokeInk(TestInks.Blue).Underline().Overline());

        RecordedPage page = LayoutHarness.Draw(element, new Extent(500, 500));

        Assert.All(page.Operations.OfType<LineOperation>(), line => Assert.Equal(TestInks.Blue, line.Ink));
        Assert.Equal(TestInks.Red, Assert.Single(page.Texts).Style.Ink);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStrokeWeightOverridesTheFontAndTheHalfPointFloor(bool fontPlacesStrokes)
    {
        TextBlock element = Text(text => text.Run("Hello").StrokeWeight(0.25f).Underline().StrikeThrough().Overline());

        List<LineOperation> lines = Strokes(element, fontPlacesStrokes);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Approximately.Equal(0.25f, line.Thickness));
    }

    [Theory]
    [InlineData(StrokeStyle.Solid)]
    [InlineData(StrokeStyle.Double)]
    [InlineData(StrokeStyle.Dotted)]
    [InlineData(StrokeStyle.Dashed)]
    [InlineData(StrokeStyle.Wavy)]
    public void EveryStrokeOfARunIsDrawnInItsStrokeStyle(StrokeStyle style)
    {
        TextBlock element = Text(text => text.Run("Hello").StrokeStyle(style).Underline().StrikeThrough().Overline());

        List<LineOperation> lines = Strokes(element, fontPlacesStrokes: false);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Equal(style, line.Style));
    }

    [Fact]
    public void AStrokeStyleAloneDrawsNothing()
    {
        TextBlock element = Text(text => text.Run("Hello").StrokeStyle(StrokeStyle.Wavy).StrokeInk(TestInks.Blue));

        Assert.Empty(Strokes(element, fontPlacesStrokes: false));
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

    [Fact]
    public void AParagraphWithNoSpansAtAllTakesNoSpace()
    {
        TextBlock element = new TextBlock { DefaultTypeRefinement = style => style.WithPointSize(40) };

        // Empty spans are a blank line someone wrote; no spans at all are no text, and leave no gap behind.
        Assert.True(LayoutHarness.Measure(element, new Extent(500, 500)).IsNothing);
    }
}
