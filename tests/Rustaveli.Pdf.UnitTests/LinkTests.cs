namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Clickable areas: external hyperlinks and links to a named section.
/// </summary>
public class LinkTests
{
    [Fact]
    public void AHyperlinkCoversExactlyItsContent()
    {
        LinkBlock element = new LinkBlock { Url = "https://example.com", Child = new FixedBlock(50, 20) };

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
        LinkBlock element = new LinkBlock { Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<ExternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void AHyperlinkAroundContentWithNothingToShowIsNotDrawn(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        LinkBlock element = new LinkBlock { Url = "https://example.com", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkCoversExactlyItsContent()
    {
        CrossReferenceBlock element = new CrossReferenceBlock { Anchor = "intro", Child = new FixedBlock(50, 20) };

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
        CrossReferenceBlock element = new CrossReferenceBlock { Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<InternalLinkOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>());
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void ASectionLinkAroundContentWithNothingToShowIsNotDrawn(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        CrossReferenceBlock element = new CrossReferenceBlock { Anchor = "intro", Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void ASectionLinkDoesNotChangeTheLayout()
    {
        CrossReferenceBlock element = new CrossReferenceBlock { Anchor = "intro", Child = new FixedBlock(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void ASectionLinkReachesItsSectionAcrossPages()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().CrossReference("appendix").Text("See the appendix");
                column.Add().NewPage();
                column.Add().Anchor("appendix").Text("Appendix");
            });
        }));

        RecordingSurface canvas = LayoutHarness.Render(document);

        Assert.Equal("appendix", Assert.Single(canvas.Page(1).Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Equal("appendix", Assert.Single(canvas.Page(2).Operations.OfType<DestinationOperation>()).Name);
    }
}
