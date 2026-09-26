namespace Rustaveli.Pdf.UnitTests;

public class ListRegressionTests
{
    [Fact]
    public void ADirectlyConstructedListStillRenders()
    {
        // Every member needed to build a list is public, so a list assembled without the fluent helper must not
        // silently render nothing.
        ListElement list = new ListElement { Marker = ListMarker.Decimal };
        list.Items.Add(new ListItem { Child = new FixedElement(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Draw(list, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void ItemsAddedAfterCompositionAreStillRendered()
    {
        Block root = LayoutHarness.Build(container => container.List(list =>
        {
            list.Item().Text("alpha");
            list.Item().Text("beta");
        }));

        ListElement element = (ListElement)((Frame)root).Child!;
        element.Items.Add(new ListItem { Child = new FixedElement(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }
}
