namespace Rustaveli.Pdf.UnitTests;

public class ListRegressionTests
{
    [Fact]
    public void ADirectlyConstructedListStillRenders()
    {
        // Every member needed to build a list is public, so a list assembled without the fluent helper must not
        // silently render nothing.
        ListBlock list = new ListBlock { Numbering = ListNumbering.Arabic };
        list.Items.Add(new ListEntry { Child = new FixedBlock(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Render(list, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void ItemsAddedAfterCompositionAreStillRendered()
    {
        Block root = LayoutHarness.Build(frame => frame.List(list =>
        {
            list.Add().Text("alpha");
            list.Add().Text("beta");
        }));

        ListBlock block = (ListBlock)((Frame)root).Child!;
        block.Items.Add(new ListEntry { Child = new FixedBlock(40, 20, TestInks.Red) });

        RecordedPage page = LayoutHarness.Render(block, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }
}
