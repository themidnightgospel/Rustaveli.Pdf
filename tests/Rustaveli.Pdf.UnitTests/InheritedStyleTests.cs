namespace Rustaveli.Pdf.UnitTests;

public class InheritedStyleTests
{
    [Fact]
    public void DefaultTextStyleReachesNestedText()
    {
        Element root = LayoutHarness.Build(container =>
            container.DefaultTextStyle(style => style.FontSizeOf(24)).Text("Hi"));

        RecordedPage page = LayoutHarness.Draw(root, new Size(500, 500));

        Approximately.Equal(24f, Assert.Single(page.Texts).Style.FontSize);
    }

    [Fact]
    public void NestedDefaultsCompose()
    {
        Element root = LayoutHarness.Build(container => container
            .DefaultTextStyle(style => style.FontSizeOf(24))
            .DefaultTextStyle(style => style.Bold())
            .Text("Hi"));

        RecordedPage page = LayoutHarness.Draw(root, new Size(500, 500));
        TextStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(24f, style.FontSize);
        Assert.Equal(FontWeight.Bold, style.Weight);
    }

    [Fact]
    public void TheDefaultIsRestoredAfterTheSubtree()
    {
        LayoutContext context = LayoutHarness.Context();
        Element root = LayoutHarness.Build(container =>
            container.DefaultTextStyle(style => style.FontSizeOf(24)).Text("Hi"));

        LayoutHarness.Draw(root, new Size(500, 500), context);

        Approximately.Equal(TextStyle.Default.FontSize, context.DefaultTextStyle.FontSize);
    }

    [Fact]
    public void TheDefaultAppliesWhileMeasuringToo()
    {
        // Measuring at one size and drawing at another would reserve the wrong amount of room for the text.
        LayoutContext context = LayoutHarness.Context();
        Element root = LayoutHarness.Build(container =>
            container.DefaultTextStyle(style => style.FontSizeOf(24)).Text("Hi"));

        SpacePlan plan = LayoutHarness.Measure(root, new Size(500, 500), context);

        // Two characters at 12pt each, on a 24pt line.
        Approximately.Equal(new Size(24, 24), plan.Size);
        Approximately.Equal(TextStyle.Default.FontSize, context.DefaultTextStyle.FontSize);
    }
}
