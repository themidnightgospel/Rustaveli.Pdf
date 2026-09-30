namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Seeing where frames lie, and being told where a layout failed: frame edges drawn over content, frames named, and
/// the path down to the frame that could not fit.
/// </summary>
public class DebuggingTests
{
    private static readonly Extent Space = new Extent(200, 100);

    [Fact]
    public void FrameEdgesOutlineTheRoomGivenOverTheContent()
    {
        RecordedPage page = LayoutHarness.Render(LayoutHarness.Build(frame => frame.ShowFrameEdges().Height(20).Fill(TestInks.Blue).Blank()), Space);

        List<LineOperation> edges = page.Operations.OfType<LineOperation>().ToList();

        Assert.Equal(4, edges.Count);
        Assert.All(edges, edge => Assert.Equal(Ink.Rgb(0xE5, 0x39, 0x35), edge.Ink));
        Assert.All(edges, edge => Assert.Equal([3f, 2f], edge.Dashes!));
        Assert.Equal(
            [new Offset(0, 0), new Offset(200, 0), new Offset(200, 100), new Offset(0, 100)],
            edges.Select(edge => edge.Position));

        // The outline is drawn after the content, so it lies over it.
        int content = page.Operations.FindIndex(operation => operation is RectangleOperation { Ink: var ink } && ink == TestInks.Blue);
        Assert.True(content >= 0 && content < page.Operations.IndexOf(edges[0]));
        Assert.Empty(page.Operations.OfType<TextOperation>());
    }

    [Fact]
    public void FrameEdgesCarryTheirLabelOnATabOfTheirInk()
    {
        RecordedPage page = LayoutHarness.Render(LayoutHarness.Build(frame => frame.ShowFrameEdges("Address", TestInks.Green).Height(40).Blank()), Space);

        RectangleOperation tab = Assert.Single(page.Operations.OfType<RectangleOperation>());
        TextOperation label = Assert.Single(page.Operations.OfType<TextOperation>());

        Assert.Equal((Ink)TestInks.Green, tab.Ink);
        Assert.Equal(Offset.Zero, tab.Position);
        Assert.Equal("Address", label.Text);
        Assert.Equal(6f, label.Style.PointSize);
        Assert.Equal(Ink.White, label.Style.Ink);

        // Seven characters at half of 6 points, and a margin of 2 either side.
        Approximately.Equal(new Extent(21 + 4, tab.Size.Height), tab.Size);
        Approximately.Equal(2f, label.Position.X);
    }

    [Fact]
    public void FrameEdgesMeasureAsTheirContentAndShowTheLabelOnEveryPage()
    {
        FrameEdgesBlock block = new FrameEdgesBlock("Box", TestInks.Red) { Child = new FixedBlock(30, 10) };

        Approximately.Equal(new Extent(30, 10), LayoutHarness.Plan(block, Space).Size);
        Assert.Equal("Box", block.Label);
        Assert.Null(new FrameEdgesBlock(null, TestInks.Red).Label);
        Assert.Equal(2, block.GetChildren().Count(child => child is not null));

        RecordingSurface surface = new RecordingSurface();
        RenderContext context = new RenderContext(surface, LayoutHarness.Context());
        surface.BeginPage(Space);
        block.Render(Space, context);
        block.Render(Space, context);
        surface.EndPage();

        Assert.Equal(2, surface.Pages[0].Operations.OfType<TextOperation>().Count());
    }

    [Fact]
    public void AFailureTracesThePathDownToTheFrameThatCouldNotFit()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Document.Compose(composition => composition.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(stack =>
            {
                stack.Add().Text("Fits");
                stack.Add().Named("Totals").Inset(5).Compose(inner => inner.Slot().Child = new FixedBlock(10, 500));
            });
        }));

        OversetException exception = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "\nWhere it did not fit, from the page down:" +
            "\n  Stack, offered 200 × 100: does not fit" +
            "\n    \"Totals\", offered 200 × 100: does not fit" +
            "\n      Inset, offered 200 × 100: does not fit" +
            "\n        Fixed, offered 190 × 90: does not fit — The block requires 10 × 500 pt " +
            "but only 190 × 90 pt is available.",
            exception.Message.Substring(exception.Message.IndexOf("\nWhere", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnUnboundedRoomIsNamedAsSuch()
    {
        PlanContext context = LayoutHarness.Context();
        context.Trace = new PlanTrace();

        new FixedBlock(10, 99_999).Plan(Extent.Max, context);

        Assert.Contains("Fixed, offered unbounded × unbounded: does not fit", context.Trace.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void NothingDeferredIsNothingToDescribe()
    {
        PlanContext context = LayoutHarness.Context();
        context.Trace = new PlanTrace();

        new FixedBlock(10, 10).Plan(Space, context);

        Assert.Equal(string.Empty, context.Trace.Describe());
        Assert.Single(context.Trace.Roots);
    }

    [Fact]
    public void MeasuringOutsideATraceRecordsNothing()
    {
        PlanTrace trace = new PlanTrace();
        PlanContext context = LayoutHarness.Context();

        new FixedBlock(10, 10).Plan(Space, context);

        Assert.Empty(trace.Roots);
        Assert.Null(context.Trace);
    }

    [Fact]
    public void AnInspectionRecordsEveryFrameDrawnWithinTheOneThatDrewItAndWhereItLies()
    {
        Document document = Document.Compose(composition => composition.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(stack =>
            {
                stack.Add().Inset(10).Named("Box").Compose(inner => inner.Slot().Child = new FixedBlock(30, 20));
                stack.Add().NewPage();
                stack.Add().Compose(inner => inner.Slot().Child = new FixedBlock(5, 5));
            });
        }));
        LayoutInspection inspection = new LayoutInspection();

        Typesetter.Render(document, new RecordingSurface(), LayoutHarness.Measurer, inspection: inspection);

        Assert.Equal(2, inspection.Pages.Count);
        LayoutInspection.Node box = Descendants(inspection.Pages[0]).Single(node => node.Name == "\"Box\"");
        LayoutInspection.Node fixedBlock = Descendants(box.Children).Single(node => node.Name == "Fixed");
        Assert.Equal(new Offset(10, 10), box.Origin);
        Assert.Equal(new Offset(10, 10), fixedBlock.Origin);
        Assert.Equal(new Extent(180, 20), fixedBlock.Size);
        Assert.Same(box, Ancestors(fixedBlock).Last(node => node.Name == "\"Box\""));
        Assert.Null(inspection.Pages[0][0].Parent);
        Assert.Contains(Descendants(inspection.Pages[1]), node => node.Name == "Fixed" && node.Origin == Offset.Zero);
        Assert.All(Descendants(inspection.Pages[0]), node => Assert.Null(node.Source));
    }

    [Fact]
    public void FramesDrawnBeforeAnyPageBelongToNone()
    {
        LayoutInspection inspection = new LayoutInspection();

        inspection.Leave(inspection.Enter(new FixedBlock(1, 1), Offset.Zero, Extent.Zero));

        Assert.Empty(inspection.Pages);
    }

    [Fact]
    public void AFrameIsInspectedByItsNameOrWhatItIs()
    {
        Assert.Equal("\"Totals\"", LayoutInspection.Name(new LabelBlock { Label = "Totals" }));
        Assert.Equal("Fixed", LayoutInspection.Name(new FixedBlock(1, 1)));
        Assert.Equal(nameof(Probe), LayoutInspection.Name(new Probe()));
    }

    [Fact]
    public void AFrameRemembersTheLineThatMadeItOnlyWhileSourcesAreRecorded()
    {
        Assert.Null(new FixedBlock(1, 1).Source);

        using (SourceCapture.Record())
        {
            // A frame made by a frame of the writer's own is traced to that frame's code, or to the line that made
            // it where the runtime folded the small constructor into it.
            Assert.Matches(@"(FixedBlock|DebuggingTests)\.cs:\d+$", new FixedBlock(1, 1).Source);
            Assert.Matches(@"DebuggingTests\.cs:\d+$", SourceCapture.Current());

            using (SourceCapture.Record())
                Assert.NotNull(SourceCapture.Current());

            // Ending an inner recording leaves the outer one on.
            Assert.NotNull(SourceCapture.Current());
        }

        Assert.Null(SourceCapture.Current());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AFrameIsNamedWithWords(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(frame => frame.Named(name!)));

    private static IEnumerable<LayoutInspection.Node> Descendants(IEnumerable<LayoutInspection.Node> nodes) =>
        nodes.SelectMany(node => Descendants(node.Children).Prepend(node));

    private static IEnumerable<LayoutInspection.Node> Ancestors(LayoutInspection.Node node)
    {
        for (LayoutInspection.Node? parent = node.Parent; parent is not null; parent = parent.Parent)
            yield return parent;
    }

    /// <summary>A frame whose kind does not end in the word every library frame ends in.</summary>
    private sealed class Probe : Block
    {
        protected override Fit PlanCore(Extent availableSpace, PlanContext context) => Fit.Complete(Extent.Zero);

        protected override void RenderCore(Extent availableSpace, RenderContext context)
        {
        }
    }
}
