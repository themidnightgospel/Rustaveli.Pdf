namespace Rustaveli.Pdf.UnitTests;

public class RequireSpaceRegressionTests
{
    [Fact]
    public void SurvivesAParentThatDrawsWithTheHeightItMeasured()
    {
        // A header measures against the whole page body and then draws with the band height it settled on.
        // Re-deriving the headroom decision from that smaller box would refuse content the parent had already
        // committed to, and the early return would drop it with no diagnostic.
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(300, 400);
            page.RunningHead().RequireSpace(100).Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Red));
            page.Body().Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Blue));
        }));

        RecordingSurface canvas = LayoutHarness.Render(document);

        Assert.Contains(canvas.Page(1).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void SurvivesBeingDrawnInsideARow()
    {
        ColumnsBlock row = new ColumnsBlock();
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Share, Child = new FixedBlock(40, 20, TestInks.Blue) });
        row.Items.Add(new ColumnSlot
        {
            Sizing = ColumnSizing.Share,
            Child = new RequireSpaceBlock { MinHeight = 100, Child = new FixedBlock(40, 20, TestInks.Red) }
        });

        RecordedPage page = LayoutHarness.Draw(row, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void APageThatDrewNothingDoesNotVoidTheGuarantee()
    {
        // Only content that actually occupied space counts as started. Otherwise a page rendering nothing would
        // permanently disarm the headroom guarantee for every page after it.
        RequireSpaceBlock element = new RequireSpaceBlock
        {
            MinHeight = 80,
            Child = new WhenBlock { Condition = false, Child = new FixedBlock(10, 10) }
        };

        LayoutHarness.Draw(element, new Extent(200, 100));

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 40)).IsDeferred);
    }
}
