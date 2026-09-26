namespace Rustaveli.Pdf.UnitTests;

public class ColumnDescriptorTests
{
    private static readonly Size Space = new Size(200, 200);

    private static void Fill(IContainer container, Color color) =>
        container.Element(inner => inner.Child = new FixedElement(10, 20, color));

    [Fact]
    public void StacksItemsInDeclarationOrder()
    {
        Element root = LayoutHarness.Build(container => container.Column(column =>
        {
            Fill(column.Item(), Colors.Red);
            Fill(column.Item(), Colors.Blue);
        }));

        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, items.Single(r => r.Color == Colors.Red).Position.Y);
        Approximately.Equal(20f, items.Single(r => r.Color == Colors.Blue).Position.Y);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Element root = LayoutHarness.Build(container => container.Column(column =>
        {
            column.Spacing(10);
            Fill(column.Item(), Colors.Red);
            Fill(column.Item(), Colors.Blue);
        }));

        SpacePlan plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items.Single(r => r.Color == Colors.Blue).Position.Y);
    }
}
