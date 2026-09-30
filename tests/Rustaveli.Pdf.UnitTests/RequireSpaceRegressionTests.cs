namespace Rustaveli.Pdf.UnitTests;

public class RequireSpaceRegressionTests
{
    [Fact]
    public void SurvivesAParentThatDrawsWithTheHeightItMeasured()
    {
        // A header measures against the whole page body and then draws with the band height it settled on.
        // Re-deriving the headroom decision from that smaller box would refuse content the parent had already
        // committed to, and the early return would drop it with no diagnostic.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(300, 400);
            page.RunningHead().RequireSpace(100).Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Red));
            page.Body().Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Blue));
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Contains(surface.Page(1).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
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

        RecordedPage page = LayoutHarness.Render(row, new Extent(300, 400));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void SurvivesAStackDrawnAtTheHeightItMeasured()
    {
        // The outer stack measures the inner one against the whole page, then draws it at the height it measured. The
        // inner stack lays its items out again in that smaller box, where the heading's headroom test would fail.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 400);
            page.Body().Stack(column =>
            {
                column.Add().Stack(chapter =>
                {
                    chapter.Add().RequireSpace(100).Text("Chapter");
                    chapter.Add().Text("intro");
                });
                column.Add().Text("after");
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Single(surface.Pages);
        Assert.Equal("Chapterintroafter", surface.Page(1).Content);
    }

    [Fact]
    public void SurvivesARowDrawnAtTheHeightItMeasured()
    {
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 400);
            page.Body().Stack(column =>
            {
                column.Add().Columns(row =>
                {
                    row.Share().RequireSpace(100).Text("Chapter");
                    row.Share().Text("side");
                });
                column.Add().Text("after");
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Single(surface.Pages);
        Assert.Equal("Chaptersideafter", surface.Page(1).Content);
    }

    [Fact]
    public void SurvivesAPageSizedToItsContent()
    {
        // The page is as tall as the body measured, far less than the room the body was measured in.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 400);
            page.Continuous = true;
            page.Body().Stack(column =>
            {
                column.Add().RequireSpace(100).Text("Chapter");
                column.Add().Text("intro");
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Single(surface.Pages);
        Assert.Equal("Chapterintro", surface.Page(1).Content);
    }

    [Fact]
    public void SurvivesATableCellDrawnAtItsRowHeight()
    {
        // A cell is measured in unlimited height and drawn at the height of its row.
        RecordedPage page = LayoutHarness.Render(frame => frame.Table(table =>
        {
            table.Columns(columns => columns.Share());
            table.Cell().Stack(cell =>
            {
                cell.Add().RequireSpace(100).Text("Chapter");
                cell.Add().Text("intro");
            });
        }), new Extent(200, 400));

        Assert.Equal("Chapterintro", page.Content);
    }

    [Fact]
    public void StillDefersInARowWhoseTallerNeighbourLeavesTooLittleRoom()
    {
        // A row draws each column at the row's height, which can be more than the column measured. Short of the
        // headroom when measured, the heading must stay short of it when drawn, not take the room the neighbour left.
        StackBlock first = new StackBlock();
        first.Items.Add(new FixedBlock(40, 20, TestInks.Blue));
        first.Items.Add(new RequireSpaceBlock { MinHeight = 60, Child = new FixedBlock(40, 20, TestInks.Red) });

        ColumnsBlock row = new ColumnsBlock();
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Share, Child = first });
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Share, Child = new FixedBlock(40, 50, TestInks.Green) });

        StackBlock page = new StackBlock();
        page.Items.Add(row);

        RecordedPage drawn = LayoutHarness.Render(page, new Extent(300, 70));

        Assert.Contains(drawn.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Blue);
        Assert.DoesNotContain(drawn.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void APageThatDrewNothingDoesNotVoidTheGuarantee()
    {
        // Only content that actually occupied space counts as started. Otherwise a page rendering nothing would
        // permanently disarm the headroom guarantee for every page after it.
        RequireSpaceBlock block = new RequireSpaceBlock
        {
            MinHeight = 80,
            Child = new WhenBlock { Condition = false, Child = new FixedBlock(10, 10) }
        };

        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 40)).IsDeferred);
    }
}
