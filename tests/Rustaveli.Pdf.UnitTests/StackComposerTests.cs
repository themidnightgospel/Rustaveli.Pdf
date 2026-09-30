namespace Rustaveli.Pdf.UnitTests;

public class StackComposerTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static void Fill(IFrame frame, Ink color) =>
        frame.Compose(inner => inner.Slot().Child = new FixedBlock(10, 20, color));

    [Fact]
    public void StacksItemsInDeclarationOrder()
    {
        Block root = LayoutHarness.Build(frame => frame.Stack(column =>
        {
            Fill(column.Add(), TestInks.Red);
            Fill(column.Add(), TestInks.Blue);
        }));

        List<RectangleOperation> items =
            LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, items.Single(r => r.Ink == TestInks.Red).Position.Y);
        Approximately.Equal(20f, items.Single(r => r.Ink == TestInks.Blue).Position.Y);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Block root = LayoutHarness.Build(frame => frame.Stack(column =>
        {
            column.SpaceBetween(10);
            Fill(column.Add(), TestInks.Red);
            Fill(column.Add(), TestInks.Blue);
        }));

        Fit plan = LayoutHarness.Plan(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items.Single(r => r.Ink == TestInks.Blue).Position.Y);
    }
}
