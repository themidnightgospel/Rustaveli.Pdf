namespace Rustaveli.Pdf.UnitTests;

public class DecorationElementTests
{
    private static DecorationElement Build(Action<DecorationDescriptor> compose)
    {
        DecorationElement element = new DecorationElement();
        compose(new DecorationDescriptor(element));
        return element;
    }

    [Fact]
    public void StacksTheBandsAroundTheContent()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 15, TestInks.Red));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 20, TestInks.Blue));
            decoration.After().Element(container => container.Child = new FixedElement(10, 25, TestInks.Green));
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles.Single(r => r.Color == TestInks.Red).Position.Y);
        Approximately.Equal(15f, rectangles.Single(r => r.Color == TestInks.Blue).Position.Y);
        Approximately.Equal(35f, rectangles.Single(r => r.Color == TestInks.Green).Position.Y);
    }

    [Fact]
    public void SumsBandAndContentHeights()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 15));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 20));
            decoration.After().Element(container => container.Child = new FixedElement(10, 25));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ReportsPartialRenderWhileContentRemains()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 10));
            decoration.Content().Element(container => container.Child = new SplittableElement(unitCount: 4, unitHeight: 20));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 50));

        Assert.True(plan.IsPartialRender);
    }

    [Fact]
    public void RepeatsBandTextOnEveryPage()
    {
        // The bands accompany the content wherever it breaks, so their text must be redrawn in full each page
        // rather than being consumed on the first.
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Text("Continued");
            decoration.Content().Element(container => container.Child = new SplittableElement(unitCount: 4, unitHeight: 20));
        });

        Size space = new Size(200, 52);

        RecordedPage firstPage = LayoutHarness.Draw(element, space);
        RecordedPage secondPage = LayoutHarness.Draw(element, space);

        Assert.Equal("Continued", firstPage.Content);
        Assert.Equal("Continued", secondPage.Content);
    }

    [Fact]
    public void TakesTheWidthOfItsWidestPart()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(80, 10));
            decoration.Content().Element(container => container.Child = new FixedElement(50, 20));
            decoration.After().Element(container => container.Child = new FixedElement(120, 5));
        });

        Approximately.Equal(120f, LayoutHarness.Measure(element, new Size(200, 200)).Size.Width);
    }

    [Fact]
    public void HandsEachPartTheSpaceLeftForIt()
    {
        ScriptedElement before = new ScriptedElement(SpacePlan.FullRender(10, 15));
        ScriptedElement content = new ScriptedElement(SpacePlan.FullRender(10, 20));
        ScriptedElement after = new ScriptedElement(SpacePlan.FullRender(10, 25));

        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = before);
            decoration.Content().Element(container => container.Child = content);
            decoration.After().Element(container => container.Child = after);
        });

        LayoutHarness.Draw(element, new Size(200, 100));

        // The content is offered everything between the bands, not just the height it reported.
        Approximately.Equal(new Size(200, 15), Assert.Single(before.DrawnWith));
        Approximately.Equal(new Size(200, 60), Assert.Single(content.DrawnWith));
        Approximately.Equal(new Size(200, 25), Assert.Single(after.DrawnWith));
    }

    [Fact]
    public void WrapsWhenTheLeadingBandDoesNotFit()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 150));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 10));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsWrap);
        Assert.Contains("not sufficient", plan.WrapReason);
    }

    [Fact]
    public void WrapsWhenTheTrailingBandDoesNotFitBelowTheLeadingOne()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 60));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 1));
            decoration.After().Element(container => container.Child = new FixedElement(10, 60));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsWrap);
        Assert.Contains("not sufficient", plan.WrapReason);
    }

    [Fact]
    public void WrapsWhenALeadingBandOverstatesItsHeight()
    {
        // A custom element can report more than it was offered. The trailing band must not then be measured
        // against a negative remainder.
        DecorationElement element = Build(decoration =>
            decoration.Before().Element(container => container.Child = new ScriptedElement(SpacePlan.FullRender(10, 150))));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsWrap);
        Assert.Contains("not sufficient", plan.WrapReason);
    }

    [Fact]
    public void WrapsWhenTheBandsLeaveNoRoomForTheContent()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 60));
            decoration.After().Element(container => container.Child = new ScriptedElement(SpacePlan.FullRender(10, 70)));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsWrap);
        Assert.Contains("no room", plan.WrapReason);
    }

    [Fact]
    public void PassesTheContentsWrapThroughUnchanged()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 10));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 200));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        // The content is measured in the 90pt left below the leading band.
        Assert.Equal(LayoutHarness.Measure(new FixedElement(10, 200), new Size(200, 90)), plan);
    }

    [Fact]
    public void ReportsEmptyOnceTheContentIsExhausted()
    {
        // The bands exist to accompany content; on their own they must not claim another page.
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 10));
            decoration.Content().Element(container => container.Child = new SplittableElement(unitCount: 1, unitHeight: 10));
        });

        Size space = new Size(200, 100);
        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsDoNotFit()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 150, TestInks.Red));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 10, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 100)).Operations);
    }

    [Fact]
    public void DrawsNothingWhenTheBandsLeaveNoRoomForTheContent()
    {
        ScriptedElement after = new ScriptedElement(SpacePlan.FullRender(10, 70));

        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 60, TestInks.Red));
            decoration.After().Element(container => container.Child = after);
        });

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 100)).Operations);
        Assert.Empty(after.DrawnWith);
    }

    [Fact]
    public void DoesNotDrawTheBandsWhenTheContentDoesNotFit()
    {
        DecorationElement element = Build(decoration =>
        {
            decoration.Before().Element(container => container.Child = new FixedElement(10, 10, TestInks.Red));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 200, TestInks.Blue));
        });

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 100)).Operations);
    }
}
