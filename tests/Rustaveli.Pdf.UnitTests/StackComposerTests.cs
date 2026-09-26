namespace Rustaveli.Pdf.UnitTests;

public class StackComposerTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static void Fill(IFrame container, Ink color) =>
        container.Compose(inner => inner.Child = new FixedBlock(10, 20, color));

    [Fact]
    public void StacksItemsInDeclarationOrder()
    {
        Block root = LayoutHarness.Build(container => container.Stack(column =>
        {
            Fill(column.Add(), TestInks.Red);
            Fill(column.Add(), TestInks.Blue);
        }));

        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, items.Single(r => r.Ink == TestInks.Red).Position.Y);
        Approximately.Equal(20f, items.Single(r => r.Ink == TestInks.Blue).Position.Y);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Block root = LayoutHarness.Build(container => container.Stack(column =>
        {
            column.SpaceBetween(10);
            Fill(column.Add(), TestInks.Red);
            Fill(column.Add(), TestInks.Blue);
        }));

        Fit plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items.Single(r => r.Ink == TestInks.Blue).Position.Y);
    }
}
