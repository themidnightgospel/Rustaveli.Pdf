namespace Rustaveli.Pdf.UnitTests;

public class RequireSpaceTests
{
    [Fact]
    public void DefersWhenTooLittleRoomRemains()
    {
        RequireSpaceBlock block = new RequireSpaceBlock { MinHeight = 80, Child = new FixedBlock(10, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 50));

        Assert.True(plan.IsDeferred);
        Assert.Contains("80", plan.DeferReason);
    }

    [Fact]
    public void ProceedsWhenEnoughRoomRemains()
    {
        RequireSpaceBlock block = new RequireSpaceBlock { MinHeight = 40, Child = new FixedBlock(10, 10) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 50)).IsComplete);
    }

    [Fact]
    public void StopsDemandingHeadroomOnceTheContentHasStarted()
    {
        // The requirement is about where content begins, not about every page it continues onto.
        RequireSpaceBlock block = new RequireSpaceBlock
        {
            MinHeight = 80,
            Child = new SplittableBlock(unitCount: 6, unitHeight: 20)
        };

        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.False(LayoutHarness.Plan(block, new Extent(200, 40)).IsDeferred);
    }

    [Fact]
    public void AGuardWithoutContentNeverCountsAsStarted()
    {
        // Drawing nothing must not disarm the guarantee for the pages that follow.
        RequireSpaceBlock block = new RequireSpaceBlock { MinHeight = 50 };

        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 30)).IsDeferred);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void ContentWithNothingToDrawNeverCountsAsStarted(string outcome)
    {
        // A parent may draw with less room than it measured in, and the content may then have nothing to show.
        RequireSpaceBlock block = new RequireSpaceBlock { MinHeight = 80, Child = ScriptedBlock.WithNothingToDraw(outcome) };

        LayoutHarness.Render(block, new Extent(200, 100));

        Assert.Contains("was required before this content may start", LayoutHarness.Plan(block, new Extent(200, 40)).DeferReason);
    }

    [Fact]
    public void MovesAHeadingToTheNextPageRatherThanStrandingIt()
    {
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 70, TestInks.Blue));
                column.Add().RequireSpace(50).Compose(inner => inner.Slot().Child = new FixedBlock(10, 10, TestInks.Red));
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Equal(2, surface.Pages.Count);
        Assert.DoesNotContain(surface.Page(1).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
        Assert.Contains(surface.Page(2).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }
}
