namespace Rustaveli.Pdf.UnitTests;

public class BandsTests
{
    private static BandsBlock Build(Action<BandsComposer> compose)
    {
        BandsBlock block = new BandsBlock();
        compose(new BandsComposer(block));
        return block;
    }

    [Fact]
    public void StacksTheBandsAroundTheContent()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 15, TestInks.Red));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 20, TestInks.Blue));
            bands.Foot().Compose(frame => frame.Slot().Child = new FixedBlock(10, 25, TestInks.Green));
        });

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles.Single(r => r.Ink == TestInks.Red).Position.Y);
        Approximately.Equal(15f, rectangles.Single(r => r.Ink == TestInks.Blue).Position.Y);
        Approximately.Equal(35f, rectangles.Single(r => r.Ink == TestInks.Green).Position.Y);
    }

    [Fact]
    public void SumsBandAndContentHeights()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 15));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 20));
            bands.Foot().Compose(frame => frame.Slot().Child = new FixedBlock(10, 25));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ReportsPartialWhileContentRemains()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10));
            bands.Body().Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 20));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 50));

        Assert.True(plan.IsPartial);
    }

    [Fact]
    public void RepeatsBandTextOnEveryPage()
    {
        // The bands accompany the content wherever it breaks, so their text must be redrawn in full each page
        // rather than being consumed on the first.
        BandsBlock block = Build(bands =>
        {
            bands.Head().Text("Continued");
            bands.Body().Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 20));
        });

        Extent space = new Extent(200, 52);

        RecordedPage firstPage = LayoutHarness.Render(block, space);
        RecordedPage secondPage = LayoutHarness.Render(block, space);

        Assert.Equal("Continued", firstPage.Content);
        Assert.Equal("Continued", secondPage.Content);
    }

    [Fact]
    public void TakesTheWidthOfItsWidestPart()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(80, 10));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(50, 20));
            bands.Foot().Compose(frame => frame.Slot().Child = new FixedBlock(120, 5));
        });

        Approximately.Equal(120f, LayoutHarness.Plan(block, new Extent(200, 200)).Size.Width);
    }

    [Fact]
    public void HandsEachPartTheSpaceLeftForIt()
    {
        ScriptedBlock before = new ScriptedBlock(Fit.Complete(10, 15));
        ScriptedBlock content = new ScriptedBlock(Fit.Complete(10, 20));
        ScriptedBlock after = new ScriptedBlock(Fit.Complete(10, 25));

        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = before);
            bands.Body().Compose(frame => frame.Slot().Child = content);
            bands.Foot().Compose(frame => frame.Slot().Child = after);
        });

        LayoutHarness.Render(block, new Extent(200, 100));

        // The content is offered everything between the bands, not just the height it reported.
        Approximately.Equal(new Extent(200, 15), Assert.Single(before.DrawnWith));
        Approximately.Equal(new Extent(200, 60), Assert.Single(content.DrawnWith));
        Approximately.Equal(new Extent(200, 25), Assert.Single(after.DrawnWith));
    }

    [Fact]
    public void DefersWhenTheLeadingBandDoesNotFit()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 150));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("too small for the head and foot bands", plan.DeferReason);
    }

    [Fact]
    public void DefersWhenTheTrailingBandDoesNotFitBelowTheLeadingOne()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 60));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 1));
            bands.Foot().Compose(frame => frame.Slot().Child = new FixedBlock(10, 60));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("too small for the head and foot bands", plan.DeferReason);
    }

    [Fact]
    public void DefersWhenALeadingBandOverstatesItsHeight()
    {
        // A custom block can report more than it was offered. The trailing band must not then be measured
        // against a negative remainder.
        BandsBlock block = Build(bands =>
            bands.Head().Compose(frame => frame.Slot().Child = new ScriptedBlock(Fit.Complete(10, 150))));

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("too small for the head and foot bands", plan.DeferReason);
    }

    [Fact]
    public void DefersWhenTheBandsLeaveNoRoomForTheContent()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 60));
            bands.Foot().Compose(frame => frame.Slot().Child = new ScriptedBlock(Fit.Complete(10, 70)));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("no room", plan.DeferReason);
    }

    [Fact]
    public void PassesTheContentsDeferralThroughUnchanged()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 200));
        });

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        // The content is measured in the 90pt left below the leading band.
        Assert.Equal(LayoutHarness.Plan(new FixedBlock(10, 200), new Extent(200, 90)), plan);
    }

    [Fact]
    public void ReportsEmptyOnceTheContentIsExhausted()
    {
        // The bands exist to accompany content; on their own they must not claim another page.
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10));
            bands.Body().Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 1, unitHeight: 10));
        });

        Extent space = new Extent(200, 100);
        LayoutHarness.Render(block, space);

        Assert.True(LayoutHarness.Plan(block, space).IsNothing);
        Assert.Empty(LayoutHarness.Render(block, space).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsDoNotFit()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 150, TestInks.Red));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsLeaveNoRoomForTheContent()
    {
        ScriptedBlock after = new ScriptedBlock(Fit.Complete(10, 70));

        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 60, TestInks.Red));
            bands.Foot().Compose(frame => frame.Slot().Child = after);
        });

        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
        Assert.Empty(after.DrawnWith);
    }

    [Fact]
    public void DoesNotDrawTheBandsWhenTheContentDoesNotFit()
    {
        BandsBlock block = Build(bands =>
        {
            bands.Head().Compose(frame => frame.Slot().Child = new FixedBlock(10, 10, TestInks.Red));
            bands.Body().Compose(frame => frame.Slot().Child = new FixedBlock(10, 200, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
    }
}
