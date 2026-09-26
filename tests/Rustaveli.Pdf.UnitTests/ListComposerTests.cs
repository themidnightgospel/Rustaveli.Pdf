namespace Rustaveli.Pdf.UnitTests;

public class ListComposerTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static Block BuildList(Action<ListComposer> compose) =>
        LayoutHarness.Build(container => container.List(compose));

    [Fact]
    public void UnorderedSwitchesANumberedListBackToBullets()
    {
        Block root = BuildList(list =>
        {
            list.Numbered();
            list.Bulleted();
            list.Add().Text("alpha");
        });

        string content = LayoutHarness.Draw(root, Space).Content;

        Assert.Contains("•", content);
        Assert.DoesNotContain("1.", content);
    }

    [Fact]
    public void MarkerStyleRestylesTheMarkersAlone()
    {
        Block root = BuildList(list =>
        {
            list.MarkerType(style => style.WithPointSize(20));
            list.Add().Text("alpha");
        });

        List<TextOperation> texts = LayoutHarness.Draw(root, Space).Texts.ToList();

        Approximately.Equal(20f, texts.Single(text => text.Text == "•").Style.PointSize);
        Approximately.Equal(TypeStyle.Default.PointSize, texts.Single(text => text.Text == "alpha").Style.PointSize);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Block root = BuildList(list =>
        {
            list.SpaceBetween(10);
            list.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 20, TestInks.Red));
            list.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 20, TestInks.Red));
        });

        Fit plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items[1].Position.Y);
    }
}
