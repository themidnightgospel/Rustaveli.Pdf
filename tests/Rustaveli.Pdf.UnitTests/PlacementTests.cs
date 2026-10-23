namespace Rustaveli.Pdf.UnitTests;

public class PlacementTests
{
    [Theory]
    [InlineData(nameof(HorizontalPlacement.Left), 0f)]
    [InlineData(nameof(HorizontalPlacement.Center), 75f)]
    [InlineData(nameof(HorizontalPlacement.Right), 150f)]
    public void PositionsTheChildHorizontally(string placement, float expectedX)
    {
        HorizontalPlacement horizontal = (HorizontalPlacement)Enum.Parse(typeof(HorizontalPlacement), placement);
        PlacementBlock block = new PlacementBlock { Horizontal = horizontal, Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedX, rectangle.Position.X);
    }

    [Theory]
    [InlineData(nameof(VerticalPlacement.Top), 0f)]
    [InlineData(nameof(VerticalPlacement.Middle), 40f)]
    [InlineData(nameof(VerticalPlacement.Bottom), 80f)]
    public void PositionsTheChildVertically(string placement, float expectedY)
    {
        VerticalPlacement vertical = (VerticalPlacement)Enum.Parse(typeof(VerticalPlacement), placement);
        PlacementBlock block = new PlacementBlock { Vertical = vertical, Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedY, rectangle.Position.Y);
    }

    [Fact]
    public void MeasuresAsItsChildDoesOnEveryAxis()
    {
        PlacementBlock block = new PlacementBlock
        {
            Horizontal = HorizontalPlacement.Center,
            Vertical = VerticalPlacement.Middle,
            Child = new FixedBlock(50, 20),
        };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void PassesTheChildsDeferralThroughUnchanged()
    {
        FixedBlock child = new FixedBlock(300, 20);
        PlacementBlock block = new PlacementBlock { Horizontal = HorizontalPlacement.Center, Child = child };
        Extent space = new Extent(200, 100);

        Fit plan = LayoutHarness.Plan(block, space);

        Assert.Equal(LayoutHarness.Plan(child, space), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChildEvenOnAlignedAxes()
    {
        // Claiming the full space for a child with nothing left would leave a blank block on every later page.
        PlacementBlock block = new PlacementBlock
        {
            Horizontal = HorizontalPlacement.Center,
            Vertical = VerticalPlacement.Middle,
            Child = new ScriptedBlock(Fit.Nothing())
        };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtItsOwnSize()
    {
        PlacementBlock block = new PlacementBlock
        {
            Horizontal = HorizontalPlacement.Right,
            Child = new SplittableBlock(unitCount: 4, unitHeight: 30, width: 40)
        };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 70));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(40, 60), plan.Size);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void DoesNotAskAChildWithNothingToShowToDraw(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        PlacementBlock block = new PlacementBlock { Horizontal = HorizontalPlacement.Center, Child = child };

        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void CombinesBothAxesIntoOneBlock()
    {
        // Chaining must not nest two aligners, or the inner one would receive an already-collapsed box.
        Block root = LayoutHarness.Build(frame => frame.FlushRight().Middle().Compose(inner =>
            inner.Slot().Child = new FixedBlock(50, 20)));

        RecordedPage page = LayoutHarness.Render(root, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Offset(150, 40), rectangle.Position);
    }

    [Theory]
    [InlineData("", "Hello ", "World", "HelloWorld")]
    [InlineData("", "Total due 9,250.00 ", "EUR", "Total due 9,250.00EUR")]
    [InlineData("Hello", " ", "World", "HelloWorld")]
    [InlineData("Hello ", " ", "World", "HelloWorld")]
    public void TextAtItsOwnWidthKeepsTheLineAfterALineEndingInASpace(string before, string line, string after, string drawn)
    {
        // The whitespace a line ends in is not counted in the paragraph's width, so at that width it no longer fits.
        RecordedPage page = LayoutHarness.Render(
            frame => frame.FlushRight().Text(text =>
            {
                text.Run(before);
                text.Line(line);
                text.Run(after);
            }),
            new Extent(400, 100));

        Assert.Equal(drawn, page.Content);
    }

    [Fact]
    public void ASpaceThatOverflowsItsBoxBeforeALineBreakAddsNoEmptyLine()
    {
        float width = LayoutHarness.Measurer.MeasureWidth("Hello", TypeStyle.Default);

        float Drop(float boxWidth)
        {
            RecordedPage page = LayoutHarness.Render(
                frame => frame.Width(boxWidth).Text(text =>
                {
                    text.Line("Hello ");
                    text.Run("World");
                }),
                new Extent(400, 100));

            return page.Texts.Single(text => text.Text == "World").Position.Y - page.Texts.Single(text => text.Text == "Hello").Position.Y;
        }

        Approximately.Equal(Drop(400), Drop(width));
    }

    [Fact]
    public void ALinkedSpaceAfterOneThatDidNotFitKeepsItsLink()
    {
        // The plain space overflows a box just wide enough for the word; the linked one then fits the next line, and a
        // space that carries a link is never dropped as trailing whitespace.
        float width = LayoutHarness.Measurer.MeasureWidth("Hello", TypeStyle.Default);

        RecordedPage page = LayoutHarness.Render(
            frame => frame.Width(width).Text(text =>
            {
                text.Run("Hello ");
                text.Link(" ", "https://example.com");
                text.Line(string.Empty);
                text.Run("World");
            }),
            new Extent(400, 100));

        Assert.Equal("https://example.com", Assert.Single(page.Operations.OfType<ExternalLinkOperation>()).Url);
        Assert.Equal("Hello World", page.Content);
    }
}
