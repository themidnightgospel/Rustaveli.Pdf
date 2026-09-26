namespace Rustaveli.Pdf.UnitTests;

public class ColumnDescriptorTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static void Fill(IFrame container, Ink color) =>
        container.Element(inner => inner.Child = new FixedElement(10, 20, color));

    [Fact]
    public void StacksItemsInDeclarationOrder()
    {
        Block root = LayoutHarness.Build(container => container.Column(column =>
        {
            Fill(column.Item(), TestInks.Red);
            Fill(column.Item(), TestInks.Blue);
        }));

        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, items.Single(r => r.Color == TestInks.Red).Position.Y);
        Approximately.Equal(20f, items.Single(r => r.Color == TestInks.Blue).Position.Y);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Block root = LayoutHarness.Build(container => container.Column(column =>
        {
            column.Spacing(10);
            Fill(column.Item(), TestInks.Red);
            Fill(column.Item(), TestInks.Blue);
        }));

        Fit plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items.Single(r => r.Color == TestInks.Blue).Position.Y);
    }
}
