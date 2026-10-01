using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Content composed page by page, and positions captured for it to look up.
/// </summary>
public class DynamicContentTests
{
    /// <summary>Numbered rows, 20 points tall, as many on each page as it holds.</summary>
    private sealed class Rows(int total) : IDynamicContent<int>
    {
        public List<(PageFacts Facts, Extent Room, int State)> Composed { get; } = [];

        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state)
        {
            Composed.Add((page.Facts, page.Room, state));

            int fits = (int)(page.Room.Height / 20);
            int taken = Math.Min(fits, total - state);

            return new DynamicPart<int>(
                frame => frame.Stack(stack =>
                {
                    for (int row = state; row < state + taken; row++)
                        stack.Add().Height(20).Text("Row " + (row + 1));
                }),
                state + taken,
                state + taken < total);
        }
    }

    private static List<RecordedPage> Render(Action<Section> configure) =>
        LayoutHarness.Render(Document.Compose(frame => frame.Section(section =>
        {
            section.Trim = new Extent(200, 100);
            configure(section);
        }))).Pages;

    [Fact]
    public void ContentIsComposedForEachPageAsFarAsItHolds()
    {
        Rows rows = new Rows(12);
        List<RecordedPage> pages = Render(section => section.Body().ComposePerPage(rows));

        Assert.Equal(3, pages.Count);
        Assert.Equal("Row 1Row 2Row 3Row 4Row 5", pages[0].Content);
        Assert.Equal("Row 11Row 12", pages[2].Content);
    }

    [Fact]
    public void EachPageKnowsItsNumberRoomAndState()
    {
        Rows rows = new Rows(12);
        Render(section => section.Body().ComposePerPage(rows));

        // The last pass draws the pages the reader sees: numbered, with the count known.
        List<(PageFacts Facts, Extent Room, int State)> final = rows.Composed.Where(composed => composed.Facts.PageCount is not null).ToList();

        Assert.Contains((new PageFacts(1, 3), new Extent(200, 100), 0), final);
        Assert.Contains((new PageFacts(2, 3), new Extent(200, 100), 5), final);
        Assert.Contains((new PageFacts(3, 3), new Extent(200, 100), 10), final);
    }

    [Fact]
    public void APageIsComposedOnceHoweverOftenItIsMeasured()
    {
        Rows rows = new Rows(3);
        DynamicBlock<int> block = new DynamicBlock<int>(rows);
        PlanContext context = LayoutHarness.Context();

        LayoutHarness.Plan(block, new Extent(200, 100), context);
        LayoutHarness.Plan(block, new Extent(200, 100), context);
        LayoutHarness.Render(block, new Extent(200, 100), context);

        Assert.Single(rows.Composed);
    }

    /// <summary>Numbered rows, 20 points tall, that keep a line free on each page for "Continued", drawn only when they go on.</summary>
    private sealed class RowsWithContinuation(int total) : IDynamicContent<int>
    {
        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state)
        {
            int taken = Math.Min((int)(page.Room.Height / 20) - 1, total - state);
            int next = state + taken;

            return new DynamicPart<int>(
                frame => frame.Stack(stack =>
                {
                    for (int row = state; row < next; row++)
                        stack.Add().Height(20).Text("Row " + (row + 1));

                    if (next < total)
                        stack.Add().Height(20).Text("Continued");
                }),
                next,
                next < total);
        }
    }

    [Fact]
    public void ContentEndingOnAPageIsDrawnAsItWasComposedForTheRoomItWasOffered()
    {
        // On the second page the last two rows fit and the content ends, 40 points tall. Composed again for that 40-point
        // box, it would keep a line free and fit only one row, and the last row would never be drawn.
        List<RecordedPage> pages = Render(section => section.Body().Stack(stack =>
        {
            stack.Add().ComposePerPage(new RowsWithContinuation(6));
            stack.Add().Text("After");
        }));

        Assert.Equal(2, pages.Count);
        Assert.Equal("Row 1Row 2Row 3Row 4Continued", pages[0].Content);
        Assert.Equal("Row 5Row 6After", pages[1].Content);
    }

    [Fact]
    public void ContentThatEndsIsCompleteAndThenNothing()
    {
        DynamicBlock<int> block = new DynamicBlock<int>(new Rows(2));

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));
        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsNothing);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
    }

    [Fact]
    public void ContentThatDoesNotFitMovesOnWithoutAdvancing()
    {
        DynamicBlock<int> block = new DynamicBlock<int>(new Fixed(new FixedBlock(500, 10)));

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsDeferred);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
    }

    [Fact]
    public void EmptyContentTakesNoRoom()
    {
        DynamicBlock<int> block = new DynamicBlock<int>(new Fixed(null));

        Assert.Equal(Extent.Zero, LayoutHarness.Plan(block, new Extent(200, 100)).Size);
    }

    [Fact]
    public void ProgressIsSavedAndRestored()
    {
        DynamicBlock<int> block = new DynamicBlock<int>(new Rows(12));
        LayoutHarness.Render(block, new Extent(200, 100));

        Progress saved = block.SaveProgress();
        string ahead = LayoutHarness.Render(block, new Extent(200, 100)).Content;
        block.RestoreProgress(saved);

        Assert.Equal(ahead, LayoutHarness.Render(block, new Extent(200, 100)).Content);
    }

    [Fact]
    public void ContentMustComposeSomething()
    {
        DynamicBlock<int> block = new DynamicBlock<int>(new Nothing());

        Assert.Throws<InvalidOperationException>(() => LayoutHarness.Plan(block, new Extent(200, 100)));
    }

    [Fact]
    public void ContentCanMeasureBeforeChoosing()
    {
        Measuring measuring = new Measuring();
        Render(section => section.Body().ComposePerPage(measuring));

        Assert.Equal(new Extent(30, 10), measuring.Fits);
        Assert.Null(measuring.TooWide);
        Assert.Equal(new Extent(30, 10), measuring.InRoom);
        Assert.Equal(ReadingDirection.LeftToRight, measuring.Direction);
        Assert.NotNull(measuring.DefaultType);
        Assert.Throws<ArgumentNullException>(() => measuring.Page!.Measure(null!));
    }

    [Fact]
    public void ContentOnEveryPageCanFindWhereOtherContentWasDrawn()
    {
        Marker marker = new Marker();
        List<RecordedPage> pages = Render(section =>
        {
            section.Margins = Sides.All(10);
            section.Overlay().ComposePerPage(marker);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(30).Blank();
                stack.Add().CapturePosition("total").Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 30));
            });
        });

        // The captured content starts 30 down the first page's body, one unit there, and carries on at the top of
        // the next two.
        Assert.Equal(3, pages.Count);
        Assert.Equal(
            [
                new CapturedPosition(1, new Offset(10, 40), new Extent(180, 30)),
                new CapturedPosition(2, new Offset(10, 10), new Extent(180, 60)),
                new CapturedPosition(3, new Offset(10, 10), new Extent(180, 30)),
            ],
            marker.Seen);
    }

    [Fact]
    public void NothingIsKnownOfPositionsNotCaptured() =>
        Assert.Empty(new Pagination().PositionsOf("anywhere"));

    [Fact]
    public void APositionNeedsAName()
    {
        Assert.Throws<ArgumentException>(() => LayoutHarness.Build(frame => frame.CapturePosition(" ")));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.ComposePerPage<int>(null!)));
    }

    [Fact]
    public void TheCountingSinkFollowsTheOrigin()
    {
        using CountingPageSink sink = new CountingPageSink();
        sink.BeginPage(new Extent(100, 100));
        sink.MoveOrigin(new Offset(10, 20));
        sink.Save();
        sink.ScaleAxes(2, 2);
        sink.MoveOrigin(new Offset(5, 5));
        Assert.Equal(new Offset(20, 30), sink.Origin);

        sink.RotateClockwise(90);
        sink.MoveOrigin(new Offset(10, 0));
        Approximately.Equal(new Offset(20, 50), sink.Origin);

        sink.Restore();
        Assert.Equal(new Offset(10, 20), sink.Origin);

        sink.BeginPage(new Extent(100, 100));
        Assert.Equal(Offset.Zero, sink.Origin);
    }

    [Fact]
    public void TheLayeredSinkFollowsTheOrigin()
    {
        using RecordingSurface pages = new RecordingSurface();
        LayeredPageSink layers = new LayeredPageSink(pages);
        layers.BeginPage(new Extent(100, 100));
        layers.MoveOrigin(new Offset(10, 20));
        layers.Save();
        layers.ScaleAxes(2, 2);
        layers.RotateClockwise(90);
        layers.MoveOrigin(new Offset(5, 0));

        Approximately.Equal(new Offset(10, 30), layers.Origin);

        layers.Restore();
        Assert.Equal(new Offset(10, 20), layers.Origin);
        layers.EndPage();
    }

    /// <summary>Always the same content, once.</summary>
    private sealed class Fixed(Block? content) : IDynamicContent<int>
    {
        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state) =>
            new DynamicPart<int>(frame => frame.Compose(inner => inner.Slot().Child = content), 1, false);
    }

    /// <summary>Returns no part at all, as a careless implementation might.</summary>
    private sealed class Nothing : IDynamicContent<int>
    {
        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state) => null!;
    }

    /// <summary>Measures candidates before drawing nothing.</summary>
    private sealed class Measuring : IDynamicContent<int>
    {
        public Extent? Fits { get; private set; }

        public Extent? TooWide { get; private set; }

        public Extent? InRoom { get; private set; }

        public ReadingDirection Direction { get; private set; }

        public TypeStyle? DefaultType { get; private set; }

        public DynamicPage? Page { get; private set; }

        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state)
        {
            Page = page;
            Fits = page.Measure(frame => frame.Compose(inner => inner.Slot().Child = new FixedBlock(30, 10)));
            TooWide = page.Measure(frame => frame.Compose(inner => inner.Slot().Child = new FixedBlock(500, 10)));
            InRoom = page.Measure(frame => frame.Compose(inner => inner.Slot().Child = new FixedBlock(30, 10)), new Extent(40, 40));
            Direction = page.ReadingDirection;
            DefaultType = page.DefaultType;
            return new DynamicPart<int>(frame => { }, 0, false);
        }
    }

    /// <summary>Looks up where the captured content was drawn, as a mark in the margin would.</summary>
    private sealed class Marker : IDynamicContent<int>
    {
        public IReadOnlyList<CapturedPosition> Seen { get; private set; } = [];

        public int Initial => 0;

        public DynamicPart<int> Compose(DynamicPage page, int state)
        {
            Seen = page.PositionsOf("total");
            return new DynamicPart<int>(frame => { }, 0, false);
        }
    }
}
