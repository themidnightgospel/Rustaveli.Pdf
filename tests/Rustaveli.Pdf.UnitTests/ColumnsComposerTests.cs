namespace Rustaveli.Pdf.UnitTests;

public class ColumnsComposerTests
{
    private static void Fill(IFrame frame, float width = 1) =>
        frame.Compose(inner => inner.Slot().Child = new FixedBlock(width, 10));

    /// <summary>The left edge of every item's content, across a 200pt row.</summary>
    private static List<float> ItemPositions(Action<ColumnsComposer> compose)
    {
        Block root = LayoutHarness.Build(frame => frame.Columns(compose));
        RecordedPage page = LayoutHarness.Render(root, new Extent(200, 100));

        return page.Operations.OfType<RectangleOperation>().Select(rectangle => rectangle.Position.X).ToList();
    }

    [Fact]
    public void SharedColumnsShareTheWidthByWeight()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.Share(1));
            Fill(row.Share(3));
        });

        // Weights 1:3 across 200pt put the second item at 50.
        Approximately.Equal(0f, positions[0]);
        Approximately.Equal(50f, positions[1]);
    }

    [Fact]
    public void SharedColumnsWithoutAWeightShareEqually()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.Share());
            Fill(row.Share());
        });

        Approximately.Equal(100f, positions[1]);
    }

    [Fact]
    public void AFixedColumnTakesExactlyItsWidth()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.Fixed(60));
            Fill(row.Share());
        });

        Approximately.Equal(60f, positions[1]);
    }

    [Fact]
    public void ANaturalColumnTakesTheWidthItsContentNeeds()
    {
        List<float> positions = ItemPositions(row =>
        {
            Fill(row.Natural(), 35);
            Fill(row.Share());
        });

        Approximately.Equal(35f, positions[1]);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        List<float> positions = ItemPositions(row =>
        {
            row.Gutter(20);
            Fill(row.Share());
            Fill(row.Share());
        });

        // 200 less 20 of spacing leaves 90 each, so the second item starts at 90 + 20.
        Approximately.Equal(110f, positions[1]);
    }
}
