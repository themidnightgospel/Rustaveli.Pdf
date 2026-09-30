namespace Rustaveli.Pdf.UnitTests;

public class ShrinkToFitRegressionTests
{
    [Fact]
    public void PassesSplittableContentThroughRatherThanFailingTheDocument()
    {
        // Content that can only ever render in instalments cannot be made to fit at any scale. Deferring it
        // makes the engine give up on the whole document; passing it through lets it paginate normally.
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new SplittableBlock(unitCount: 50, unitHeight: 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.False(plan.IsDeferred);
        Assert.True(plan.IsPartial);
    }

    [Fact]
    public void LongDocumentContentStillPaginates()
    {
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().ShrinkToFit().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 40, unitHeight: 20));
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.True(surface.Pages.Count > 1);
    }

    [Fact]
    public void AMinimumScaleOfOneBehavesAsThoughTheBlockWereAbsent()
    {
        // "Never shrink" makes this a pass-through, not an unsatisfiable constraint: whatever the child would
        // have reported on its own is what comes back.
        Extent space = new Extent(200, 100);

        ShrinkToFitBlock wrapped = new ShrinkToFitBlock { MinScale = 1f, Child = new FixedBlock(120, 50) };
        FixedBlock bare = new FixedBlock(120, 50);

        Approximately.Equal(
            LayoutHarness.Plan(bare, space).Size,
            LayoutHarness.Plan(wrapped, space).Size);

        Assert.True(LayoutHarness.Plan(wrapped, space).IsComplete);
    }

    [Fact]
    public void AMinimumScaleOfZeroMeansNoLowerBound()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { MinScale = 0f, Child = new FixedBlock(400, 300) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsComplete);
    }
}
