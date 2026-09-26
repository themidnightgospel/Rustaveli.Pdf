namespace Rustaveli.Pdf.UnitTests;

public class KeepTogetherTests
{
    [Fact]
    public void ConvertsAPartialRenderIntoAWrap()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void LeavesContentThatFitsAlone()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 2, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void DrawsNothingWhenItWouldHaveToSplit()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 50));

        Assert.Empty(page.Operations);
    }

    [Fact]
    public void MovesContentWholeToTheNextPage()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Child = new FixedBlock(10, 60));
                column.Add().KeepTogether().Compose(inner => inner.Child = new FixedBlock(10, 60));
            });
        }));

        RecordingSurface canvas = LayoutHarness.Render(document);

        // Each page also carries the white page background, so count only the content blocks.
        IEnumerable<RectangleOperation> firstPage = canvas.Page(1).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);
        IEnumerable<RectangleOperation> secondPage = canvas.Page(2).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
    }
}
