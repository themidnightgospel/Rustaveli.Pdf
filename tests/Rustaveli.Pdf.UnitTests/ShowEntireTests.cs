namespace Rustaveli.Pdf.UnitTests;

public class ShowEntireTests
{
    [Fact]
    public void ConvertsAPartialRenderIntoAWrap()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableElement(unitCount: 4, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void LeavesContentThatFitsAlone()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableElement(unitCount: 2, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsFullRender);
    }

    [Fact]
    public void DrawsNothingWhenItWouldHaveToSplit()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableElement(unitCount: 4, unitHeight: 25) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 50));

        Assert.Empty(page.Operations);
    }

    [Fact]
    public void MovesContentWholeToTheNextPage()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(200, 100);
            page.Content().Column(column =>
            {
                column.Item().Element(inner => inner.Child = new FixedElement(10, 60));
                column.Item().KeepTogether().Element(inner => inner.Child = new FixedElement(10, 60));
            });
        }));

        RecordingCanvas canvas = LayoutHarness.Render(document);

        // Each page also carries the white page background, so count only the content blocks.
        IEnumerable<RectangleOperation> firstPage = canvas.Page(1).Operations.OfType<RectangleOperation>().Where(r => r.Color == TestInks.Black);
        IEnumerable<RectangleOperation> secondPage = canvas.Page(2).Operations.OfType<RectangleOperation>().Where(r => r.Color == TestInks.Black);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
    }
}
