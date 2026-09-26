namespace Rustaveli.Pdf.UnitTests;

public class ListDescriptorTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static Block BuildList(Action<ListComposer> compose) =>
        LayoutHarness.Build(container => container.List(compose));

    [Fact]
    public void UnorderedSwitchesANumberedListBackToBullets()
    {
        Block root = BuildList(list =>
        {
            list.Ordered();
            list.Unordered();
            list.Item().Text("alpha");
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
            list.MarkerStyle(style => style.FontSizeOf(20));
            list.Item().Text("alpha");
        });

        List<TextOperation> texts = LayoutHarness.Draw(root, Space).Texts.ToList();

        Approximately.Equal(20f, texts.Single(text => text.Text == "•").Style.FontSize);
        Approximately.Equal(TypeStyle.Default.FontSize, texts.Single(text => text.Text == "alpha").Style.FontSize);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Block root = BuildList(list =>
        {
            list.Spacing(10);
            list.Item().Element(inner => inner.Child = new FixedElement(10, 20, TestInks.Red));
            list.Item().Element(inner => inner.Child = new FixedElement(10, 20, TestInks.Red));
        });

        Fit plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items[1].Position.Y);
    }
}
