namespace Rustaveli.Pdf.UnitTests;

public class ListRegressionTests
{
    [Fact]
    public void ADirectlyConstructedListStillRenders()
    {
        // Every member needed to build a list is public, so a list assembled without the fluent helper must not
        // silently render nothing.
        ListBlock list = new ListBlock { Marker = ListNumbering.Arabic };
        list.Items.Add(new ListEntry { Child = new FixedElement(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Draw(list, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void ItemsAddedAfterCompositionAreStillRendered()
    {
        Block root = LayoutHarness.Build(container => container.List(list =>
        {
            list.Add().Text("alpha");
            list.Add().Text("beta");
        }));

        ListBlock element = (ListBlock)((Frame)root).Child!;
        element.Items.Add(new ListEntry { Child = new FixedElement(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }
}
