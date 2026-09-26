namespace Rustaveli.Pdf.UnitTests;

public class RowDescriptorTests
{
    private static void Fill(IFrame container, float width = 1) =>
        container.Element(inner => inner.Child = new FixedElement(width, 10));

    /// <summary>The left edge of every item's content, across a 200pt row.</summary>
    private static List<float> ItemPositions(Action<RowDescriptor> compose)
    {
        Block root = LayoutHarness.Build(container => container.Row(compose));
        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 100));

        return page.Operations.OfType<RectangleOperation>().Select(rectangle => rectangle.Position.X).ToList();
    }

    [Fact]
    public void RelativeItemsShareTheWidthByWeight()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.RelativeItem(1));
            Fill(row.RelativeItem(3));
        });

        // Weights 1:3 across 200pt put the second item at 50.
        Approximately.Equal(0f, positions[0]);
        Approximately.Equal(50f, positions[1]);
    }

    [Fact]
    public void RelativeItemsWithoutAWeightShareEqually()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.RelativeItem());
            Fill(row.RelativeItem());
        });

        Approximately.Equal(100f, positions[1]);
    }

    [Fact]
    public void AConstantItemTakesExactlyItsWidth()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.ConstantItem(60));
            Fill(row.RelativeItem());
        });

        Approximately.Equal(60f, positions[1]);
    }

    [Fact]
    public void AnAutoItemTakesTheWidthItsContentNeeds()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.AutoItem(), 35);
            Fill(row.RelativeItem());
        });

        Approximately.Equal(35f, positions[1]);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        List<float> positions = ItemPositions(row =>
        {
            row.Spacing(20);
            Fill(row.RelativeItem());
            Fill(row.RelativeItem());
        });

        // 200 less 20 of spacing leaves 90 each, so the second item starts at 90 + 20.
        Approximately.Equal(110f, positions[1]);
    }
}
