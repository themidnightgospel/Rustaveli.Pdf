namespace Rustaveli.Pdf.UnitTests;

public class ListDescriptorTests
{
    private static readonly Size Space = new Size(200, 200);

    private static Element BuildList(Action<ListDescriptor> compose) =>
        LayoutHarness.Build(container => container.List(compose));

    [Fact]
    public void UnorderedSwitchesANumberedListBackToBullets()
    {
        Element root = BuildList(list =>
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
        Element root = BuildList(list =>
        {
            list.MarkerStyle(style => style.FontSizeOf(20));
            list.Item().Text("alpha");
        });

        List<TextOperation> texts = LayoutHarness.Draw(root, Space).Texts.ToList();

        Approximately.Equal(20f, texts.Single(text => text.Text == "•").Style.FontSize);
        Approximately.Equal(TextStyle.Default.FontSize, texts.Single(text => text.Text == "alpha").Style.FontSize);
    }

    [Fact]
    public void SpacingSeparatesConsecutiveItems()
    {
        Element root = BuildList(list =>
        {
            list.Spacing(10);
            list.Item().Element(inner => inner.Child = new FixedElement(10, 20, Colors.Red));
            list.Item().Element(inner => inner.Child = new FixedElement(10, 20, Colors.Red));
        });

        SpacePlan plan = LayoutHarness.Measure(root, Space);
        List<RectangleOperation> items =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(50f, plan.Size.Height);
        Approximately.Equal(30f, items[1].Position.Y);
    }
}
