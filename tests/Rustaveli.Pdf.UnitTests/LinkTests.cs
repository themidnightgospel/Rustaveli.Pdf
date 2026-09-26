namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Clickable areas: external hyperlinks and links to a named section.
/// </summary>
public class LinkTests
{
    [Fact]
    public void AHyperlinkCoversExactlyItsContent()
    {
        HyperlinkElement element = new HyperlinkElement { Url = "https://example.com", Child = new FixedElement(50, 20) };

        // Offered exactly the content's size, so the clickable box is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        ExternalLinkOperation link = Assert.Single(page.Operations.OfType<ExternalLinkOperation>());

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal(new Bounds(0, 0, 50, 20), link.Bounds);
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void AHyperlinkWithoutAUrlDrawsOnlyItsContent()
    {
        HyperlinkElement element = new HyperlinkElement { Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations.OfType<ExternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void AHyperlinkAroundContentWithNothingToShowIsNotDrawn(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        HyperlinkElement element = new HyperlinkElement { Url = "https://example.com", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkCoversExactlyItsContent()
    {
        InternalLinkElement element = new InternalLinkElement { DestinationName = "intro", Child = new FixedElement(50, 20) };

        // Offered exactly the content's size, so the clickable box is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        InternalLinkOperation link = Assert.Single(page.Operations.OfType<InternalLinkOperation>());

        Assert.Equal("intro", link.Destination);
        Assert.Equal(new Bounds(0, 0, 50, 20), link.Bounds);
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void ASectionLinkWithoutADestinationDrawsOnlyItsContent()
    {
        InternalLinkElement element = new InternalLinkElement { Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations.OfType<InternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void ASectionLinkAroundContentWithNothingToShowIsNotDrawn(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        InternalLinkElement element = new InternalLinkElement { DestinationName = "intro", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkDoesNotChangeTheLayout()
    {
        InternalLinkElement element = new InternalLinkElement { DestinationName = "intro", Child = new FixedElement(50, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(50, 20), plan.Size);
    }

    [Fact]
    public void ASectionLinkReachesItsSectionAcrossPages()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 100);
            page.Content().Column(column =>
            {
                column.Item().SectionLink("appendix").Text("See the appendix");
                column.Item().PageBreak();
                column.Item().Section("appendix").Text("Appendix");
            });
        }));

        RecordingCanvas canvas = LayoutHarness.Render(document);

        Assert.Equal("appendix", Assert.Single(canvas.Page(1).Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Equal("appendix", Assert.Single(canvas.Page(2).Operations.OfType<DestinationOperation>()).Name);
    }
}
