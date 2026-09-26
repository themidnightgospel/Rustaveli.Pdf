namespace Rustaveli.Pdf.UnitTests;

public class LayersTests
{
    [Fact]
    public void OverlayLayersRepeatOnEveryPage()
    {
        // A watermark accompanies its content onto every page. Its text tracks how much of itself it has drawn,
        // so without a per-page reset it would be consumed on page one and trail off mid-word on page two.
        LayersBlock element = new LayersBlock();

        Layer primary = new Layer { IsBase = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 20, width: 200) };
        Layer overlay = new Layer();
        ((IFrame)overlay).Text("mark");

        element.Layers.Add(primary);
        element.Layers.Add(overlay);

        Extent space = new Extent(200, 40);

        RecordedPage firstPage = LayoutHarness.Draw(element, space);
        RecordedPage secondPage = LayoutHarness.Draw(element, space);

        Assert.Equal("mark", firstPage.Content);
        Assert.Equal("mark", secondPage.Content);
    }

    [Fact]
    public void TakesItsSizeFromTheBaseLayerAndPaintsInDeclarationOrder()
    {
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new PlaceholderBlock { Ink = TestInks.Red } });
        element.Layers.Add(new Layer { IsBase = true, Child = new FixedBlock(50, 20, TestInks.Black) });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock { Ink = TestInks.Blue } });

        Extent space = new Extent(200, 200);
        Fit plan = LayoutHarness.Measure(element, space);
        List<RectangleOperation> painted = LayoutHarness.Draw(element, space).Operations.OfType<RectangleOperation>().ToList();

        Ink[] backgroundContentOverlay = [TestInks.Red, TestInks.Black, TestInks.Blue];

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 20), plan.Size);
        Assert.Equal(backgroundContentOverlay, painted.Select(operation => operation.Ink));
        Assert.All(painted, operation => Approximately.Equal(Offset.Zero, operation.Position));
    }

    [Fact]
    public void WithoutABaseLayerOccupiesNothing()
    {
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new FixedBlock(50, 20) });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

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
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });
        element.Layers.Add(new Layer { IsBase = true, Child = content });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(content.DrawnWith);
    }

    [Fact]
    public void TheBaseLayerContinuesWhereItStopped()
    {
        // Only the decorating layers repeat; the content itself must not restart on every page.
        SplittableBlock content = new SplittableBlock(unitCount: 4, unitHeight: 20);
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { IsBase = true, Child = content });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        Extent space = new Extent(200, 40);
        LayoutHarness.Draw(element, space);
        LayoutHarness.Draw(element, space);

        Assert.Equal(0, content.Remaining);
        Assert.True(LayoutHarness.Measure(element, space).IsNothing);
    }

    [Fact]
    public void AFullResetRewindsTheBaseLayer()
    {
        SplittableBlock content = new SplittableBlock(unitCount: 4, unitHeight: 20);
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { IsBase = true, Child = content });

        LayoutHarness.Draw(element, new Extent(200, 40));
        element.ResetState();

        Assert.Equal(4, content.Remaining);
    }
}
