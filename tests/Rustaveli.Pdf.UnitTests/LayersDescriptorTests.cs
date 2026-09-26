namespace Rustaveli.Pdf.UnitTests;

public class LayersDescriptorTests
{
    private static readonly Size Space = new Size(200, 200);

    private static void Fill(IContainer container, float width, float height, Color color) =>
        container.Element(inner => inner.Child = new FixedElement(width, height, color));

    [Fact]
    public void ThePrimaryLayerSizesTheStack()
    {
        Element root = LayoutHarness.Build(container => container.Layers(layers =>
        {
            layers.Layer().Placeholder(Colors.Red);
            Fill(layers.PrimaryLayer(), 50, 20, Colors.Blue);
        }));

        Approximately.Equal(new Size(50, 20), LayoutHarness.Measure(root, Space).Size);
    }

    [Fact]
    public void AnOrdinaryLayerContributesNothingToTheSize()
    {
        Element root = LayoutHarness.Build(container => container.Layers(layers =>
            Fill(layers.Layer(), 50, 20, Colors.Blue)));

        Approximately.Equal(Size.Zero, LayoutHarness.Measure(root, Space).Size);
    }

    [Fact]
    public void PaintsLayersInDeclarationOrder()
    {
        Element root = LayoutHarness.Build(container => container.Layers(layers =>
        {
            layers.Layer().Placeholder(Colors.Red);
            Fill(layers.PrimaryLayer(), 50, 20, Colors.Blue);
            layers.Layer().Placeholder(Colors.Green);
        }));

        List<RectangleOperation> rectangles =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        // Layers before the primary one sit underneath it and those after it on top. How far each layer extends
        // is the stack's painting business, not the descriptor's, so only the order is asserted.
        Assert.Equal(3, rectangles.Count);
        Assert.Equal((Color)Colors.Red, rectangles[0].Color);
        Assert.Equal((Color)Colors.Blue, rectangles[1].Color);
        Assert.Equal((Color)Colors.Green, rectangles[2].Color);
    }
}
