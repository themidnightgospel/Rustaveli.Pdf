namespace Rustaveli.Pdf.UnitTests;

public class LayersTests
{
    [Fact]
    public void OverlayLayersRepeatOnEveryPage()
    {
        // A watermark accompanies its content onto every page. Its text tracks how much of itself it has drawn,
        // so without a per-page reset it would be consumed on page one and trail off mid-word on page two.
        LayersBlock block = new LayersBlock();

        Layer baseLayer = new Layer { IsBase = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 20, width: 200) };
        Layer overlay = new Layer();
        ((IFrame)overlay).Text("mark");

        block.Layers.Add(baseLayer);
        block.Layers.Add(overlay);

        Extent space = new Extent(200, 40);

        RecordedPage firstPage = LayoutHarness.Render(block, space);
        RecordedPage secondPage = LayoutHarness.Render(block, space);

        Assert.Equal("mark", firstPage.Content);
        Assert.Equal("mark", secondPage.Content);
    }

    [Fact]
    public void TakesItsSizeFromTheBaseLayerAndPaintsInDeclarationOrder()
    {
        LayersBlock block = new LayersBlock();
        block.Layers.Add(new Layer { Child = new PlaceholderBlock { Ink = TestInks.Red } });
        block.Layers.Add(new Layer { IsBase = true, Child = new FixedBlock(50, 20, TestInks.Black) });
        block.Layers.Add(new Layer { Child = new PlaceholderBlock { Ink = TestInks.Blue } });

        Extent space = new Extent(200, 200);
        Fit plan = LayoutHarness.Plan(block, space);
        List<RectangleOperation> painted = LayoutHarness.Render(block, space).Operations.OfType<RectangleOperation>().ToList();

        Ink[] backgroundContentOverlay = [TestInks.Red, TestInks.Black, TestInks.Blue];

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 20), plan.Size);
        Assert.Equal(backgroundContentOverlay, painted.Select(operation => operation.Ink));
        Assert.All(painted, operation => Approximately.Equal(Offset.Zero, operation.Position));
    }

    [Fact]
    public void WithoutABaseLayerOccupiesNothing()
    {
        LayersBlock block = new LayersBlock();
        block.Layers.Add(new Layer { Child = new FixedBlock(50, 20) });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void DrawsNoLayerWhenTheBaseLayerHasNothingToShow(string outcome)
    {
        // A watermark without its page content would be a page of watermark alone.
        ScriptedBlock content = ScriptedBlock.WithNothingToDraw(outcome);
        LayersBlock block = new LayersBlock();
        block.Layers.Add(new Layer { Child = new PlaceholderBlock() });
        block.Layers.Add(new Layer { IsBase = true, Child = content });
        block.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(content.DrawnWith);
    }

    [Fact]
    public void TheBaseLayerContinuesWhereItStopped()
    {
        // Only the decorating layers repeat; the content itself must not restart on every page.
        SplittableBlock content = new SplittableBlock(unitCount: 4, unitHeight: 20);
        LayersBlock block = new LayersBlock();
        block.Layers.Add(new Layer { IsBase = true, Child = content });
        block.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        Extent space = new Extent(200, 40);
        LayoutHarness.Render(block, space);
        LayoutHarness.Render(block, space);

        Assert.Equal(0, content.Remaining);
        Assert.True(LayoutHarness.Plan(block, space).IsNothing);
    }

    [Fact]
    public void AFullResetRewindsTheBaseLayer()
    {
        SplittableBlock content = new SplittableBlock(unitCount: 4, unitHeight: 20);
        LayersBlock block = new LayersBlock();
        block.Layers.Add(new Layer { IsBase = true, Child = content });

        LayoutHarness.Render(block, new Extent(200, 40));
        block.ResetState();

        Assert.Equal(4, content.Remaining);
    }
}
