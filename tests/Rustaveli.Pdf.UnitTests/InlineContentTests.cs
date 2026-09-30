using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Elements placed among the words of a paragraph.
/// </summary>
/// <remarks>
/// Against <see cref="FakeTypeMeasurer"/>: characters are 6pt wide and lines 12pt tall at the default size.
/// </remarks>
public class InlineContentTests
{
    private const float LineHeight = 12f;

    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock block = new TextBlock();
        compose(new TextComposer(block));
        return block;
    }

    [Fact]
    public void AnInlineFrameIsDrawnAmongTheWords()
    {
        TextBlock block = Text(text =>
        {
            text.Run("before");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 10, TestInks.Red));
            text.Run("after");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Six characters at 6pt, so the block starts right after "before".
        Approximately.Equal(36f, box.Position.X);
    }

    [Fact]
    public void TextAfterAnInlineFrameContinuesPastIt()
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 10));
            text.Run("cd");
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        List<TextOperation> drawn = page.Texts.ToList();

        Approximately.Equal(0f, drawn[0].Position.X);
        Approximately.Equal(32f, drawn[1].Position.X);
    }

    [Fact]
    public void ItsWidthCountsTowardsTheLine()
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 10));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        Approximately.Equal(32f, plan.Size.Width);
    }

    [Fact]
    public void ATallInlineFrameRaisesTheLineItLandsOn()
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 40));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        // The block occupies 40pt above the baseline; the text beside it still hangs its 2.4pt descender
        // below, so the line is the sum rather than just the taller of the two.
        Approximately.Equal(42.4f, plan.Size.Height);
    }

    [Fact]
    public void AShortInlineFrameLeavesTheLineSpacingAlone()
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 4));
        });

        Approximately.Equal(LineHeight, LayoutHarness.Plan(block, new Extent(500, 500)).Size.Height);
    }

    [Fact]
    public void ItRestsOnTheBaseline()
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 6, TestInks.Red));
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        RectangleOperation box = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        // Baseline is 80% of the 12pt line; a 6pt block sits directly above it.
        Approximately.Equal(9.6f - 6f, box.Position.Y);
    }

    /// <summary>
    /// Draws "ab" with a 6pt-tall frame after it, placed as given, and returns the frame's top and the baseline.
    /// </summary>
    private static (float FrameTop, float Baseline, float LineHeight) Place(InlinePosition position, float frameHeight)
    {
        TextBlock block = Text(text =>
        {
            text.Run("ab");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, frameHeight, TestInks.Red), position);
        });

        float height = LayoutHarness.Plan(block, new Extent(500, 500)).Size.Height;
        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        RectangleOperation box = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        return (box.Position.Y, Assert.Single(page.Texts).Position.Y, height);
    }

    [Theory]
    [InlineData(InlinePosition.OnBaseline, 3.6f, 12f)]
    [InlineData(InlinePosition.BelowBaseline, 9.6f, 15.6f)]
    [InlineData(InlinePosition.TextTop, 0f, 12f)]
    [InlineData(InlinePosition.TextBottom, 6f, 12f)]
    [InlineData(InlinePosition.Middle, 3f, 12f)]
    public void ASmallFrameSitsWhereItsPositionSays(InlinePosition position, float top, float lineHeight)
    {
        // The type reaches 9.6pt above the baseline and 2.4pt below it, so its middle is 3.6pt above.
        (float frameTop, float baseline, float height) = Place(position, 6);

        Approximately.Equal(9.6f, baseline);
        Approximately.Equal(top, frameTop);
        Approximately.Equal(lineHeight, height);
    }

    [Theory]
    [InlineData(InlinePosition.OnBaseline, 40f, 42.4f)]
    [InlineData(InlinePosition.BelowBaseline, 9.6f, 49.6f)]
    [InlineData(InlinePosition.TextTop, 9.6f, 40f)]
    [InlineData(InlinePosition.TextBottom, 37.6f, 40f)]
    [InlineData(InlinePosition.Middle, 23.6f, 40f)]
    public void ATallFrameDeepensTheLineOnTheSideItReachesPastTheType(InlinePosition position, float baseline, float lineHeight)
    {
        (float frameTop, float drawnBaseline, float height) = Place(position, 40);

        Approximately.Equal(baseline, drawnBaseline);
        Approximately.Equal(lineHeight, height);
        Approximately.Equal(position == InlinePosition.BelowBaseline ? baseline : 0f, frameTop);
    }

    [Theory]
    [InlineData(InlinePosition.OnBaseline)]
    [InlineData(InlinePosition.BelowBaseline)]
    [InlineData(InlinePosition.TextTop)]
    [InlineData(InlinePosition.TextBottom)]
    [InlineData(InlinePosition.Middle)]
    public void AFrameAloneOnItsLineMakesALineOfItsOwnHeight(InlinePosition position)
    {
        TextBlock block = Text(text =>
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 6, TestInks.Red), position));

        float height = LayoutHarness.Plan(block, new Extent(500, 500)).Size.Height;
        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, box.Position.Y);
        Approximately.Equal(6f, height);
    }

    [Fact]
    public void AFrameIsPlacedAgainstTheTallestTypeOnItsLine()
    {
        TextBlock block = Text(text =>
        {
            text.Run("a");
            text.Run("B").PointSize(24);
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 6, TestInks.Red), InlinePosition.TextTop);
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, box.Position.Y);
        Approximately.Equal(19.2f, page.Texts.First().Position.Y);
    }

    [Fact]
    public void ItMovesToTheNextLineWholeRatherThanBeingSplit()
    {
        TextBlock block = Text(text =>
        {
            text.Run("aaaa");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(20, 10, TestInks.Red));
        });

        // 24pt of text plus a 20pt block exceeds 30pt, so the block wraps.
        RecordedPage page = LayoutHarness.Render(block, new Extent(30, 500));
        RectangleOperation box = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(0f, box.Position.X);
        Assert.True(box.Position.Y > 0, "The block should have moved onto the second line.");
    }

    [Fact]
    public void AnInlineFrameCanCarryALink()
    {
        TextBlock block = new TextBlock();
        Frame frame = new Frame();
        frame.Slot().Child = new FixedBlock(20, 10);
        block.Runs.Add(new Text.TextRun { Inline = frame, Url = "https://example.com" });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));

        Assert.Single(page.Operations.OfType<ExternalLinkOperation>());
    }

    [Fact]
    public void InlineFramesAreResetBetweenPasses()
    {
        // The paragraph must expose them as children, or their pagination state would survive a new pass.
        SplittableBlock splittable = new SplittableBlock(unitCount: 2, unitHeight: 10);
        TextBlock block = new TextBlock();
        Frame frame = new Frame { Child = splittable };
        block.Runs.Add(new Text.TextRun { Inline = frame });

        LayoutHarness.Render(block, new Extent(500, 500));
        Assert.Equal(0, splittable.Remaining);

        block.ResetState();

        Assert.Equal(2, splittable.Remaining);
    }

    [Fact]
    public void APlainParagraphIsUnaffected()
    {
        TextBlock block = Text(text => text.Run("hello"));

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 500));

        Approximately.Equal(30f, plan.Size.Width);
        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void AParagraphKeepsEveryWordWhenAnInlineFramePrecedesANewPage()
    {
        // Regression: Draw re-runs BuildLines on every page and re-measures each inline block. An inline
        // block already drawn on page 1 plans to nothing on page 2, so its run is dropped, every later line
        // shifts up by one, and _completedLines — an index into the *old* wrapping — skips a line of text.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(60, 13);
            page.Margins = Sides.All(0);
            page.Body().Text(text =>
            {
                text.Inline(inline => inline.Text(nested => nested.Run("IIIIIIIIII")));
                text.Run("aaaaa bbbbb ccccc");
            });
        }));

        List<RecordedPage> pages = LayoutHarness.Render(document).Pages;

        Assert.Equal(["IIIIIIIIII", "aaaaa", "bbbbb", "ccccc"], pages.Select(page => page.Content));
    }

    [Fact]
    public void AnInlineFrameTooWideForItsLineIsReportedRatherThanDropped()
    {
        // Regression: the block used to be skipped outright, so the logo vanished from the document and the
        // paragraph still claimed to be complete — content lost with nothing to show for it.
        Block block = LayoutHarness.Build(frame => frame.Text(text =>
        {
            text.Run("logo:");
            text.Inline(inline => inline.Width(200).Height(20));
        }));

        Assert.True(LayoutHarness.Plan(block, new Extent(100, 200)).IsDeferred);
    }

    [Fact]
    public void AnInlineFrameGivenAnExplicitHeightSitsAmongTheWords()
    {
        // Regression: inline frames were planned against Size.Max.Height, so a bounded block nested under
        // one that fills its space reported 14400pt and made the paragraph impossible to place at all.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(300, 200);
            page.Margins = Sides.All(10);
            page.Body().Text(text =>
            {
                text.Run("An icon ");
                text.Inline(inline => inline.Width(10).Height(10).Middle());
                text.Run(" follows.");
            });
        }));

        Assert.Equal("An icon  follows.", Assert.Single(LayoutHarness.Render(document).Pages).Content);
    }

    [Fact]
    public void AnInlineFrameThatFillsItsHeightIsNamedInTheDiagnostic()
    {
        // It cannot be placed — it claims the whole page by definition — but the error used to blame the text
        // height, which sends the reader looking at font sizes instead of at the block.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(300, 200);
            page.Margins = Sides.All(10);
            page.Body().Text(text =>
            {
                text.Run("An icon ");
                text.Inline(inline => inline.ExpandVertically().Width(10).Height(10));
            });
        }));

        OversetException exception = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Contains(
            "A line holding an inline frame is taller than the space available. Content that expands to fill the space "
            + "offered to it, such as Expand, claims the whole page when set inline — give it an explicit Height instead.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnInlineFrameWiderThanTheLineSaysSo()
    {
        Fit plan = LayoutHarness.Plan(Text(text => text.Inline(inline => inline.Width(500).Height(10))), new Extent(100, 100));

        Assert.Equal(
            "A paragraph holds an inline frame that does not fit the width available to it. Inline frames cannot be split "
            + "across lines, so it has to fit on one.",
            plan.DeferReason);
    }

    [Fact]
    public void AnIndentTakingTheWholeWidthSaysSo()
    {
        Fit plan = LayoutHarness.Plan(Text(text =>
        {
            text.FirstLineIndent(100);
            text.Run("Words");
        }), new Extent(100, 100));

        Assert.Equal("There is no width available for text once the first-line indent is applied.", plan.DeferReason);
    }

    [Fact]
    public void AParagraphBlockedByAnInlineFrameDrawsNothing()
    {
        // Planning has deferred the paragraph. Drawing the lines completed before the blocker would split the paragraph
        // across two pages and then repeat those lines on the next.
        TextBlock block = Text(text =>
        {
            text.Line("aaa");
            text.Inline(inline => inline.Slot().Child = new FixedBlock(200, 20));
        });

        Assert.Empty(LayoutHarness.Render(block, new Extent(100, 200)).Operations);
    }

    [Fact]
    public void AnInlineFrameCanCrossReferenceAnAnchor()
    {
        TextBlock block = new TextBlock();
        block.Runs.Add(new Text.TextRun { Text = "ab" });
        block.Runs.Add(new Text.TextRun { Inline = new Frame { Child = new FixedBlock(20, 10) }, Anchor = "intro" });

        RecordedPage page = LayoutHarness.Render(block, new Extent(500, 500));
        InternalLinkOperation link = Assert.Single(page.Operations.OfType<InternalLinkOperation>());

        // The block starts after the two 6pt characters and rests on a baseline its own 10pt height sets.
        Assert.Equal("intro", link.Destination);
        Assert.Equal(new Bounds(12, 0, 32, 10), link.Bounds);
    }

    [Fact]
    public void AnInlineFrameOnAContinuationLineIsOfferedTheFullWidth()
    {
        // Only a paragraph's opening line is indented, so a block landing on a later line may use the whole
        // width. The placeholder takes whatever width it is offered, which makes the budget visible.
        TextBlock block = new TextBlock { FirstLineIndent = 20 };
        block.Runs.Add(new Text.TextRun { Text = "aaaa bbbb" });
        block.Runs.Add(new Text.TextRun
        {
            Inline = new Frame { Child = new ConstraintBlock { MaxHeight = 10, Child = new PlaceholderBlock() } }
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(60, 500));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, box.Position.X);
        Approximately.Equal(60f, box.Size.Width);
    }

    [Fact]
    public void AnInlineFrameIsSetWhileCountingPages()
    {
        // A pass that only counts pages passes lines of plain text over, but not a frame among the words, which may
        // record where it lands.
        ScriptedBlock frame = new ScriptedBlock(Fit.Complete(20, 10));
        TextBlock block = Text(text =>
        {
            text.Run("a line of plain text\n");
            text.Run("before");
            text.Inline(inline => inline.Slot().Child = frame);
            text.Run("after");
        });

        using CountingPageSink counting = new CountingPageSink();
        counting.BeginPage(new Extent(500, 500));
        block.Render(new Extent(500, 500), new RenderContext(counting, LayoutHarness.Context()));

        Assert.Single(frame.DrawnWith);
    }

#if NET
    [Fact]
    public void LinesOfPlainTextCostNothingWhileCountingPages()
    {
        TextBlock block = Text(text => text.Run(string.Join("\n", Enumerable.Repeat("a line of plain text", 40))));
        PlanContext layout = LayoutHarness.Context();
        Extent space = new Extent(500, 1000);
        LayoutHarness.Plan(block, space, layout);

        using CountingPageSink counting = new CountingPageSink();
        counting.BeginPage(space);
        long before = GC.GetAllocatedBytesForCurrentThread();
        block.Render(space, new RenderContext(counting, layout));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // What drawing costs — the pieces of each line, joined — is not paid for lines no one sees.
        Assert.True(allocated < 1024, $"{allocated} bytes allocated counting forty lines.");
    }
#endif
}
