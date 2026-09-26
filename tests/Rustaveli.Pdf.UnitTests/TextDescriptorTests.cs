namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Paragraph composition against <see cref="FakeTextMeasurer"/>: characters are 6pt wide and lines 12pt tall
/// at the default size.
/// </summary>
public class TextDescriptorTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static RecordedPage Draw(Action<TextComposer> compose, Pagination? page = null) =>
        LayoutHarness.Draw(
            LayoutHarness.Build(container => container.Text(compose)),
            Space,
            LayoutHarness.Context(page));

    [Fact]
    public void SectionLinkMakesTheRunJumpToTheSection()
    {
        RecordedPage page = Draw(text => text.CrossReference("Summary", "summary"));
        InternalLinkOperation link = Assert.Single(page.Operations.OfType<InternalLinkOperation>());

        Assert.Equal("Summary", page.Content);
        Assert.Equal("summary", link.Destination);

        // The clickable area covers the run: seven characters wide and one line tall.
        Approximately.Equal(new Extent(42, 12), link.Size);
    }

    [Fact]
    public void PageNumberOfSectionShowsWhereTheSectionLanded()
    {
        Pagination context = new Pagination();
        context.RegisterDestination("summary", 4);

        Assert.Equal("4", Draw(text => text.PageNumberOfSection("summary"), context).Content);
    }

    [Fact]
    public void PageNumberOfSectionShowsAPlaceholderUntilTheSectionIsReached() =>
        Assert.Equal("?", Draw(text => text.PageNumberOfSection("summary")).Content);

    [Fact]
    public void TotalPagesShowsTheDocumentTotal() =>
        Assert.Equal("9", Draw(text => text.TotalPages(), new Pagination { TotalPages = 9 }).Content);

    [Fact]
    public void AlignLeftOverridesTheRightToLeftDefault()
    {
        Block aligned = LayoutHarness.Build(container => container.RightToLeft().Text(text =>
        {
            text.FlushLeft();
            text.Span("Hello");
        }));
        Block unaligned = LayoutHarness.Build(container => container.RightToLeft().Text("Hello"));

        // Right-to-left text hugs the right edge unless told otherwise: 200 less five 6pt characters.
        Approximately.Equal(0f, Assert.Single(LayoutHarness.Draw(aligned, Space).Texts).Position.X);
        Approximately.Equal(170f, Assert.Single(LayoutHarness.Draw(unaligned, Space).Texts).Position.X);
    }

    [Fact]
    public void DefaultTextStyleReachesEverySpan()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.FontSizeOf(20));
            text.Span("a");
            text.Span(" b");
        });

        Assert.Equal("a b", page.Content);
        Assert.All(page.Texts, run => Approximately.Equal(20f, run.Style.FontSize));
    }

    [Fact]
    public void DefaultTextStylesCompose()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.FontSizeOf(20));
            text.DefaultType(style => style.Bold());
            text.Span("a");
        });

        TypeStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(20f, style.FontSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void ALaterDefaultTextStyleOverridesAnEarlierOne()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.FontSizeOf(10));
            text.DefaultType(style => style.FontSizeOf(20));
            text.Span("a");
        });

        Approximately.Equal(20f, Assert.Single(page.Texts).Style.FontSize);
    }

    [Fact]
    public void ASpanRefinesTheParagraphDefault()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.FontSizeOf(20));
            text.Span("a").Bold();
        });

        TypeStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(20f, style.FontSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void ElementRefusesAMissingHandler()
    {
        TextBlock element = new TextBlock();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new TextComposer(element).Element(null!));

        Assert.Equal("handler", exception.ParamName);
        Assert.Empty(element.Spans);
    }
}
