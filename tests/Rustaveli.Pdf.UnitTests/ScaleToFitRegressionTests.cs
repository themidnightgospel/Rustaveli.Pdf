namespace Rustaveli.Pdf.UnitTests;

public class ScaleToFitRegressionTests
{
    [Fact]
    public void PassesSplittableContentThroughRatherThanFailingTheDocument()
    {
        // Content that can only ever render in instalments cannot be made to fit at any scale. Reporting Wrap
        // makes the engine give up on the whole document; passing it through lets it paginate normally.
        ShrinkToFitBlock element = new ShrinkToFitBlock { Child = new SplittableElement(unitCount: 50, unitHeight: 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.False(plan.IsWrap);
        Assert.True(plan.IsPartialRender);
    }

    [Fact]
    public void LongDocumentContentStillPaginates()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(200, 100);
            page.Content().ScaleToFit().Element(inner => inner.Child = new SplittableElement(unitCount: 40, unitHeight: 20));
        }));

        RecordingCanvas canvas = LayoutHarness.Render(document);

        Assert.True(canvas.Pages.Count > 1);
    }

    [Fact]
    public void AMinimumScaleOfOneBehavesAsThoughTheElementWereAbsent()
    {
        // "Never shrink" makes this a pass-through, not an unsatisfiable constraint: whatever the child would
        // have reported on its own is what comes back.
        Extent space = new Extent(200, 100);

        ShrinkToFitBlock wrapped = new ShrinkToFitBlock { MinScale = 1f, Child = new FixedElement(120, 50) };
        FixedElement bare = new FixedElement(120, 50);

        Approximately.Equal(
            LayoutHarness.Measure(bare, space).Size,
            LayoutHarness.Measure(wrapped, space).Size);

        Assert.True(LayoutHarness.Measure(wrapped, space).IsFullRender);
    }

    [Fact]
    public void AMinimumScaleOfZeroMeansNoLowerBound()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { MinScale = 0f, Child = new FixedElement(400, 300) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsFullRender);
    }
}
