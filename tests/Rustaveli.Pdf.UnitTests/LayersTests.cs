namespace Rustaveli.Pdf.UnitTests;

public class LayersTests
{
    [Fact]
    public void OverlayLayersRepeatOnEveryPage()
    {
        // A watermark accompanies its content onto every page. Its text tracks how much of itself it has drawn,
        // so without a per-page reset it would be consumed on page one and trail off mid-word on page two.
        LayersBlock element = new LayersBlock();

        Layer primary = new Layer { IsPrimary = true, Child = new SplittableElement(unitCount: 4, unitHeight: 20, width: 200) };
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
    public void TakesItsSizeFromThePrimaryLayerAndPaintsInDeclarationOrder()
    {
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new PlaceholderBlock { Color = TestInks.Red } });
        element.Layers.Add(new Layer { IsPrimary = true, Child = new FixedElement(50, 20, TestInks.Black) });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock { Color = TestInks.Blue } });

        Extent space = new Extent(200, 200);
        Fit plan = LayoutHarness.Measure(element, space);
        List<RectangleOperation> painted = LayoutHarness.Draw(element, space).Operations.OfType<RectangleOperation>().ToList();

        Ink[] backgroundContentOverlay = [TestInks.Red, TestInks.Black, TestInks.Blue];

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(50, 20), plan.Size);
        Assert.Equal(backgroundContentOverlay, painted.Select(operation => operation.Color));
        Assert.All(painted, operation => Approximately.Equal(Offset.Zero, operation.Position));
    }

    [Fact]
    public void WithoutAPrimaryLayerOccupiesNothing()
    {
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new FixedElement(50, 20) });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Extent.Zero, plan.Size);
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void DrawsNoLayerWhenThePrimaryHasNothingToShow(FitKind outcome)
    {
        // A watermark without its page content would be a page of watermark alone.
        ScriptedElement content = ScriptedElement.WithNothingToDraw(outcome);
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });
        element.Layers.Add(new Layer { IsPrimary = true, Child = content });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(content.DrawnWith);
    }

    [Fact]
    public void ThePrimaryLayerContinuesWhereItStopped()
    {
        // Only the decorating layers repeat; the content itself must not restart on every page.
        SplittableElement content = new SplittableElement(unitCount: 4, unitHeight: 20);
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { IsPrimary = true, Child = content });
        element.Layers.Add(new Layer { Child = new PlaceholderBlock() });

        Extent space = new Extent(200, 40);
        LayoutHarness.Draw(element, space);
        LayoutHarness.Draw(element, space);

        Assert.Equal(0, content.Remaining);
        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
    }

    [Fact]
    public void AFullResetRewindsThePrimaryLayer()
    {
        SplittableElement content = new SplittableElement(unitCount: 4, unitHeight: 20);
        LayersBlock element = new LayersBlock();
        element.Layers.Add(new Layer { IsPrimary = true, Child = content });

        LayoutHarness.Draw(element, new Extent(200, 40));
        element.ResetState();

        Assert.Equal(4, content.Remaining);
    }
}
