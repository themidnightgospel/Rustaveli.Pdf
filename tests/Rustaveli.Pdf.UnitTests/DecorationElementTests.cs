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
            decoration.Before().Element(container => container.Child = new FixedElement(10, 15, Colors.Red));
            decoration.Content().Element(container => container.Child = new FixedElement(10, 20, Colors.Blue));
            decoration.After().Element(container => container.Child = new FixedElement(10, 25, Colors.Green));
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles.Single(r => r.Color == Colors.Red).Position.Y);
        Approximately.Equal(15f, rectangles.Single(r => r.Color == Colors.Blue).Position.Y);
        Approximately.Equal(35f, rectangles.Single(r => r.Color == Colors.Green).Position.Y);
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
}
