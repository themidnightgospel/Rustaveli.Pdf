namespace Rustaveli.Pdf.UnitTests;

public class InheritedTypeTests
{
    [Fact]
    public void DefaultTypeReachesNestedText()
    {
        Block root = LayoutHarness.Build(frame =>
            frame.DefaultType(style => style.WithPointSize(24)).Text("Hi"));

        RecordedPage page = LayoutHarness.Render(root, new Extent(500, 500));

        Approximately.Equal(24f, Assert.Single(page.Texts).Style.PointSize);
    }

    [Fact]
    public void NestedDefaultsCompose()
    {
        Block root = LayoutHarness.Build(frame => frame
            .DefaultType(style => style.WithPointSize(24))
            .DefaultType(style => style.Bold())
            .Text("Hi"));

        RecordedPage page = LayoutHarness.Render(root, new Extent(500, 500));
        TypeStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(24f, style.PointSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void TheDefaultIsRestoredAfterTheSubtree()
    {
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(frame =>
            frame.DefaultType(style => style.WithPointSize(24)).Text("Hi"));

        LayoutHarness.Render(root, new Extent(500, 500), context);

        Approximately.Equal(TypeStyle.Default.PointSize, context.DefaultType.PointSize);
    }

    [Fact]
    public void TheDefaultAppliesWhileMeasuringToo()
    {
        // Measuring at one size and drawing at another would reserve the wrong amount of room for the text.
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(frame =>
            frame.DefaultType(style => style.WithPointSize(24)).Text("Hi"));

        Fit plan = LayoutHarness.Plan(root, new Extent(500, 500), context);

        // Two characters at 12pt each, on a 24pt line.
        Approximately.Equal(new Extent(24, 24), plan.Size);
        Approximately.Equal(TypeStyle.Default.PointSize, context.DefaultType.PointSize);
    }
}
