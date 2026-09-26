namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Clickable areas: external hyperlinks and links to a named section.
/// </summary>
public class LinkTests
{
    [Fact]
    public void AHyperlinkCoversExactlyItsContent()
    {
        LinkBlock element = new LinkBlock { Url = "https://example.com", Child = new FixedElement(50, 20) };

        // Offered exactly the content's size, so the clickable box is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        ExternalLinkOperation link = Assert.Single(page.Operations.OfType<ExternalLinkOperation>());

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal(new Bounds(0, 0, 50, 20), link.Bounds);
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void AHyperlinkWithoutAUrlDrawsOnlyItsContent()
    {
        LinkBlock element = new LinkBlock { Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<ExternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void AHyperlinkAroundContentWithNothingToShowIsNotDrawn(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        LinkBlock element = new LinkBlock { Url = "https://example.com", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkCoversExactlyItsContent()
    {
        CrossReferenceBlock element = new CrossReferenceBlock { DestinationName = "intro", Child = new FixedElement(50, 20) };

        // Offered exactly the content's size, so the clickable box is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        InternalLinkOperation link = Assert.Single(page.Operations.OfType<InternalLinkOperation>());

        Assert.Equal("intro", link.Destination);
        Assert.Equal(new Bounds(0, 0, 50, 20), link.Bounds);
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void ASectionLinkWithoutADestinationDrawsOnlyItsContent()
    {
        CrossReferenceBlock element = new CrossReferenceBlock { Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<InternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void ASectionLinkAroundContentWithNothingToShowIsNotDrawn(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        CrossReferenceBlock element = new CrossReferenceBlock { DestinationName = "intro", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkDoesNotChangeTheLayout()
    {
        CrossReferenceBlock element = new CrossReferenceBlock { DestinationName = "intro", Child = new FixedElement(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void ASectionLinkReachesItsSectionAcrossPages()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(200, 100);
            page.Content().Column(column =>
            {
                column.Item().CrossReference("appendix").Text("See the appendix");
                column.Item().NewPage();
                column.Item().Anchor("appendix").Text("Appendix");
            });
        }));

        RecordingCanvas canvas = LayoutHarness.Render(document);

        Assert.Equal("appendix", Assert.Single(canvas.Page(1).Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Equal("appendix", Assert.Single(canvas.Page(2).Operations.OfType<DestinationOperation>()).Name);
    }
}
