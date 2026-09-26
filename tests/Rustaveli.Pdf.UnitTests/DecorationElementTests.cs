namespace Rustaveli.Pdf.UnitTests;

public class DecorationElementTests
{
    private static BandsBlock Build(Action<BandsComposer> compose)
    {
        BandsBlock element = new BandsBlock();
        compose(new BandsComposer(element));
        return element;
    }

    [Fact]
    public void StacksTheBandsAroundTheContent()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 15, TestInks.Red));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 20, TestInks.Blue));
            decoration.Foot().Compose(container => container.Child = new FixedElement(10, 25, TestInks.Green));
        });

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles.Single(r => r.Color == TestInks.Red).Position.Y);
        Approximately.Equal(15f, rectangles.Single(r => r.Color == TestInks.Blue).Position.Y);
        Approximately.Equal(35f, rectangles.Single(r => r.Color == TestInks.Green).Position.Y);
    }

    [Fact]
    public void SumsBandAndContentHeights()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 15));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 20));
            decoration.Foot().Compose(container => container.Child = new FixedElement(10, 25));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ReportsPartialRenderWhileContentRemains()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 10));
            decoration.Body().Compose(container => container.Child = new SplittableElement(unitCount: 4, unitHeight: 20));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsPartial);
    }

    [Fact]
    public void RepeatsBandTextOnEveryPage()
    {
        // The bands accompany the content wherever it breaks, so their text must be redrawn in full each page
        // rather than being consumed on the first.
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Text("Continued");
            decoration.Body().Compose(container => container.Child = new SplittableElement(unitCount: 4, unitHeight: 20));
        });

        Extent space = new Extent(200, 52);

        RecordedPage firstPage = LayoutHarness.Draw(element, space);
        RecordedPage secondPage = LayoutHarness.Draw(element, space);

        Assert.Equal("Continued", firstPage.Content);
        Assert.Equal("Continued", secondPage.Content);
    }

    [Fact]
    public void TakesTheWidthOfItsWidestPart()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(80, 10));
            decoration.Body().Compose(container => container.Child = new FixedElement(50, 20));
            decoration.Foot().Compose(container => container.Child = new FixedElement(120, 5));
        });

        Approximately.Equal(120f, LayoutHarness.Measure(element, new Extent(200, 200)).Size.Width);
    }

    [Fact]
    public void HandsEachPartTheSpaceLeftForIt()
    {
        ScriptedElement before = new ScriptedElement(Fit.Complete(10, 15));
        ScriptedElement content = new ScriptedElement(Fit.Complete(10, 20));
        ScriptedElement after = new ScriptedElement(Fit.Complete(10, 25));

        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = before);
            decoration.Body().Compose(container => container.Child = content);
            decoration.Foot().Compose(container => container.Child = after);
        });

        LayoutHarness.Draw(element, new Extent(200, 100));

        // The content is offered everything between the bands, not just the height it reported.
        Approximately.Equal(new Extent(200, 15), Assert.Single(before.DrawnWith));
        Approximately.Equal(new Extent(200, 60), Assert.Single(content.DrawnWith));
        Approximately.Equal(new Extent(200, 25), Assert.Single(after.DrawnWith));
    }

    [Fact]
    public void WrapsWhenTheLeadingBandDoesNotFit()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 150));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 10));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("not sufficient", plan.DeferReason);
    }

    [Fact]
    public void WrapsWhenTheTrailingBandDoesNotFitBelowTheLeadingOne()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 60));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 1));
            decoration.Foot().Compose(container => container.Child = new FixedElement(10, 60));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("not sufficient", plan.DeferReason);
    }

    [Fact]
    public void WrapsWhenALeadingBandOverstatesItsHeight()
    {
        // A custom element can report more than it was offered. The trailing band must not then be measured
        // against a negative remainder.
        BandsBlock element = Build(decoration =>
            decoration.Head().Compose(container => container.Child = new ScriptedElement(Fit.Complete(10, 150))));

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("not sufficient", plan.DeferReason);
    }

    [Fact]
    public void WrapsWhenTheBandsLeaveNoRoomForTheContent()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 60));
            decoration.Foot().Compose(container => container.Child = new ScriptedElement(Fit.Complete(10, 70)));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("no room", plan.DeferReason);
    }

    [Fact]
    public void PassesTheContentsWrapThroughUnchanged()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 10));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 200));
        });

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        // The content is measured in the 90pt left below the leading band.
        Assert.Equal(LayoutHarness.Measure(new FixedElement(10, 200), new Extent(200, 90)), plan);
    }

    [Fact]
    public void ReportsEmptyOnceTheContentIsExhausted()
    {
        // The bands exist to accompany content; on their own they must not claim another page.
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 10));
            decoration.Body().Compose(container => container.Child = new SplittableElement(unitCount: 1, unitHeight: 10));
        });

        Extent space = new Extent(200, 100);
        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsNothing);
        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsDoNotFit()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 150, TestInks.Red));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 10, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 100)).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsLeaveNoRoomForTheContent()
    {
        ScriptedElement after = new ScriptedElement(Fit.Complete(10, 70));

        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 60, TestInks.Red));
            decoration.Foot().Compose(container => container.Child = after);
        });

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 100)).Operations);
        Assert.Empty(after.DrawnWith);
    }

    [Fact]
    public void DoesNotDrawTheBandsWhenTheContentDoesNotFit()
    {
        BandsBlock element = Build(decoration =>
        {
            decoration.Head().Compose(container => container.Child = new FixedElement(10, 10, TestInks.Red));
            decoration.Body().Compose(container => container.Child = new FixedElement(10, 200, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 100)).Operations);
    }
}
