namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Items set side by side and wrapped into lines, in a box 100 points wide.
/// </summary>
public class FlowTests
{
    private static readonly Extent Space = new Extent(100, 200);

    private static Block Flow(Action<FlowComposer> configure, params (float Width, float Height)[] items) =>
        LayoutHarness.Build(frame => frame.Flow(flow =>
        {
            configure(flow);

            foreach ((float width, float height) in items)
                flow.Add().Compose(inner => inner.Slot().Child = new FixedBlock(width, height, TestInks.Red));
        }));

    private static List<Offset> Positions(Block flow, Extent? space = null) =>
        LayoutHarness.Draw(flow, space ?? Space).Operations.OfType<RectangleOperation>().Select(operation => operation.Position).ToList();

    [Fact]
    public void ItemsWrapWhereTheNextWouldNotFit()
    {
        Block flow = Flow(flow => flow.Gutter(10), (40, 10), (40, 20), (40, 10));

        Assert.Equal(new Extent(90, 30), LayoutHarness.Measure(flow, Space).Size);
        Assert.Equal([new Offset(0, 0), new Offset(50, 0), new Offset(0, 20)], Positions(flow));
    }

    [Fact]
    public void LinesAreSpacedApart()
    {
        Block flow = Flow(flow => flow.SpaceBetweenLines(5), (60, 10), (60, 10));

        Assert.Equal([new Offset(0, 0), new Offset(0, 15)], Positions(flow));
    }

    [Theory]
    [InlineData("left", 0f, 30f)]
    [InlineData("centre", 20f, 50f)]
    [InlineData("right", 40f, 70f)]
    [InlineData("spaced", 10f, 60f)]
    public void EachLineIsPlacedAcrossTheWidth(string placement, float first, float second)
    {
        Block flow = Flow(
            flow =>
            {
                switch (placement)
                {
                    case "centre": flow.Centered(); break;
                    case "right": flow.FlushRight(); break;
                    case "spaced": flow.SpacedAround(); break;
                    default: flow.FlushLeft(); break;
                }
            },
            (30, 10),
            (30, 10));

        Assert.Equal([new Offset(first, 0), new Offset(second, 0)], Positions(flow));
    }

    [Fact]
    public void JustifiedLinesSpanTheWidthButTheLastDoesNot()
    {
        Block flow = Flow(flow => flow.Justified(), (30, 10), (30, 10), (30, 10), (20, 10), (20, 10));

        // The first line's three items reach both edges; the last line keeps its gaps as set.
        Assert.Equal(
            [new Offset(0, 0), new Offset(35, 0), new Offset(70, 0), new Offset(0, 10), new Offset(20, 10)],
            Positions(flow));
    }

    [Fact]
    public void ALineOfOneItemIsNotSpread()
    {
        Block flow = Flow(flow => flow.Justified(), (70, 10), (70, 10));

        Assert.Equal([new Offset(0, 0), new Offset(0, 10)], Positions(flow));
    }

    [Theory]
    [InlineData("top", 0f)]
    [InlineData("middle", 10f)]
    [InlineData("bottom", 20f)]
    public void ShortItemsSitInTheirLineAsTheFlowSays(string alignment, float top)
    {
        Block flow = Flow(
            flow =>
            {
                switch (alignment)
                {
                    case "middle": flow.Middle(); break;
                    case "bottom": flow.FlushBottom(); break;
                    default: flow.FlushTop(); break;
                }
            },
            (30, 30),
            (30, 10));

        Assert.Equal(top, Positions(flow)[1].Y);
    }

    [Fact]
    public void RightToLeftItemsFlowFromTheRight()
    {
        Block flow = LayoutHarness.Build(frame => frame.RightToLeft().Flow(flow =>
        {
            flow.Gutter(10);
            flow.Add().Compose(inner => inner.Slot().Child = new FixedBlock(40, 10, TestInks.Red));
            flow.Add().Compose(inner => inner.Slot().Child = new FixedBlock(20, 10, TestInks.Red));
        }));

        Assert.Equal([new Offset(60, 0), new Offset(30, 0)], Positions(flow));
    }

    [Fact]
    public void LinesThatDoNotFitGoOnToTheNextPage()
    {
        Block flow = Flow(flow => { }, (60, 30), (60, 30), (60, 30));
        Extent page = new Extent(100, 70);

        Assert.True(LayoutHarness.Measure(flow, page).IsPartial);
        Assert.Equal(2, Positions(flow, page).Count);

        Assert.True(LayoutHarness.Measure(flow, page).IsComplete);
        Assert.Single(Positions(flow, page));

        Assert.True(LayoutHarness.Measure(flow, page).IsNothing);
        Assert.Empty(Positions(flow, page));
    }

    [Fact]
    public void AnItemThatFitsNowhereMovesTheFlowOn()
    {
        Block flow = Flow(flow => { }, (160, 10));

        Assert.True(LayoutHarness.Measure(flow, Space).IsDeferred);
    }

    [Fact]
    public void ALineTooTallForThePageMovesTheFlowOn()
    {
        Block flow = Flow(flow => { }, (60, 30));

        Assert.True(LayoutHarness.Measure(flow, new Extent(100, 20)).IsDeferred);
    }

    [Fact]
    public void AnItemThatWouldSplitWaitsForTheNextLine()
    {
        FlowBlock flow = new FlowBlock();
        flow.Items.Add(new FixedBlock(40, 10, TestInks.Red));
        flow.Items.Add(new SplittableBlock(unitCount: 5, unitHeight: 30));

        Fit plan = LayoutHarness.Measure(flow, new Extent(100, 60));

        Assert.True(plan.IsPartial);
        Assert.Equal(new Extent(40, 10), plan.Size);
    }

    [Fact]
    public void ItemsWithNothingToShowTakeNoPlace()
    {
        FlowBlock flow = new FlowBlock { Gutter = 10 };
        flow.Items.Add(new WhenBlock { Condition = false, Child = new FixedBlock(40, 10) });
        flow.Items.Add(new FixedBlock(40, 10, TestInks.Red));
        flow.Items.Add(new OnceBlock { Child = new FixedBlock(40, 10) });
        flow.Items.Add(new FixedBlock(40, 10, TestInks.Red));

        // The hidden item and, once shown, the used-up one leave only the gutter between the two that remain.
        LayoutHarness.Draw(flow, Space);
        flow.ResetState(includeDocumentProgress: false);

        Assert.Equal([new Offset(0, 0), new Offset(50, 0)], LayoutHarness.Draw(flow, Space).Operations.OfType<RectangleOperation>().Select(operation => operation.Position));
    }

    [Fact]
    public void AFlowOfNothingIsNothing()
    {
        FlowBlock flow = new FlowBlock();
        flow.Items.Add(new OnceBlock { Child = new FixedBlock(10, 10) });
        LayoutHarness.Draw(flow, Space);
        flow.ResetState(includeDocumentProgress: false);

        Assert.True(LayoutHarness.Measure(flow, Space).IsNothing);
        Assert.True(LayoutHarness.Measure(new FlowBlock(), Space).IsNothing);
        Assert.Empty(LayoutHarness.Draw(new FlowBlock(), Space).Operations);
    }

    [Fact]
    public void AFlowNeedsItsItems() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Flow(null!)));
}
