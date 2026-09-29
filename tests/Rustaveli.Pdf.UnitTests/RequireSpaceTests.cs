namespace Rustaveli.Pdf.UnitTests;

public class RequireSpaceTests
{
    [Fact]
    public void DefersWhenTooLittleRoomRemains()
    {
        RequireSpaceBlock element = new RequireSpaceBlock { MinHeight = 80, Child = new FixedBlock(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsDeferred);
        Assert.Contains("80", plan.DeferReason);
    }

    [Fact]
    public void ProceedsWhenEnoughRoomRemains()
    {
        RequireSpaceBlock element = new RequireSpaceBlock { MinHeight = 40, Child = new FixedBlock(10, 10) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 50)).IsComplete);
    }

    [Fact]
    public void StopsDemandingHeadroomOnceTheContentHasStarted()
    {
        // The requirement is about where content begins, not about every page it continues onto.
        RequireSpaceBlock element = new RequireSpaceBlock
        {
            MinHeight = 80,
            Child = new SplittableBlock(unitCount: 6, unitHeight: 20)
        };

        LayoutHarness.Draw(element, new Extent(200, 100));

        Assert.False(LayoutHarness.Measure(element, new Extent(200, 40)).IsDeferred);
    }

    [Fact]
    public void AGuardWithoutContentNeverCountsAsStarted()
    {
        // Drawing nothing must not disarm the guarantee for the pages that follow.
        RequireSpaceBlock element = new RequireSpaceBlock { MinHeight = 50 };

        LayoutHarness.Draw(element, new Extent(200, 100));

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 30)).IsDeferred);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void ContentWithNothingToDrawNeverCountsAsStarted(string outcome)
    {
        // A parent may draw with less room than it measured in, and the content may then have nothing to show.
        RequireSpaceBlock element = new RequireSpaceBlock { MinHeight = 80, Child = ScriptedBlock.WithNothingToDraw(outcome) };

        LayoutHarness.Draw(element, new Extent(200, 100));

        Assert.Contains("was required before this content may start", LayoutHarness.Measure(element, new Extent(200, 40)).DeferReason);
    }

    [Fact]
    public void MovesAHeadingToTheNextPageRatherThanStrandingIt()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 70, TestInks.Blue));
                column.Add().RequireSpace(50).Compose(inner => inner.Slot().Child = new FixedBlock(10, 10, TestInks.Red));
            });
        }));

        RecordingSurface canvas = LayoutHarness.Render(document);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.DoesNotContain(canvas.Page(1).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
        Assert.Contains(canvas.Page(2).Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }
}
