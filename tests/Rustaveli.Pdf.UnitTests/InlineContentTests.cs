using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Elements placed among the words of a paragraph.
/// </summary>
/// <remarks>
/// Against <see cref="FakeTextMeasurer"/>: characters are 6pt wide and lines 12pt tall at the default size.
/// </remarks>
public class InlineContentTests
{
    private const float LineHeight = 12f;

    private static TextElement Text(Action<TextDescriptor> compose)
    {
        TextElement element = new TextElement();
        compose(new TextDescriptor(element));
        return element;
    }

    [Fact]
    public void AnInlineElementIsDrawnAmongTheWords()
    {
        TextElement element = Text(text =>
        {
            text.Span("before");
            text.Element(inline => inline.Child = new FixedElement(20, 10, Colors.Red));
            text.Span("after");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Six characters at 6pt, so the element starts right after "before".
        Approximately.Equal(36f, block.Position.X);
    }

    [Fact]
    public void TextAfterAnInlineElementContinuesPastIt()
    {
        TextElement element = Text(text =>
        {
            text.Span("ab");
            text.Element(inline => inline.Child = new FixedElement(20, 10));
            text.Span("cd");
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        List<TextOperation> drawn = page.Texts.ToList();

        Approximately.Equal(0f, drawn[0].Position.X);
        Approximately.Equal(32f, drawn[1].Position.X);
    }

    [Fact]
    public void ItsWidthCountsTowardsTheLine()
    {
        TextElement element = Text(text =>
        {
            text.Span("ab");
            text.Element(inline => inline.Child = new FixedElement(20, 10));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(32f, plan.Size.Width);
    }

    [Fact]
    public void ATallElementRaisesTheLineItLandsOn()
    {
        TextElement element = Text(text =>
        {
            text.Span("ab");
            text.Element(inline => inline.Child = new FixedElement(20, 40));
        });

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        // The element occupies 40pt above the baseline; the text beside it still hangs its 2.4pt descender
        // below, so the line is the sum rather than just the taller of the two.
        Approximately.Equal(42.4f, plan.Size.Height);
    }

    [Fact]
    public void AShortElementLeavesTheLineHeightAlone()
    {
        TextElement element = Text(text =>
        {
            text.Span("ab");
            text.Element(inline => inline.Child = new FixedElement(20, 4));
        });

        Approximately.Equal(LineHeight, LayoutHarness.Measure(element, new Size(500, 500)).Size.Height);
    }

    [Fact]
    public void ItRestsOnTheBaseline()
    {
        TextElement element = Text(text =>
        {
            text.Span("ab");
            text.Element(inline => inline.Child = new FixedElement(20, 6, Colors.Red));
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        RectangleOperation block = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == Colors.Red);

        // Baseline is 80% of the 12pt line; a 6pt element sits directly above it.
        Approximately.Equal(9.6f - 6f, block.Position.Y);
    }

    [Fact]
    public void ItMovesToTheNextLineWholeRatherThanBeingSplit()
    {
        TextElement element = Text(text =>
        {
            text.Span("aaaa");
            text.Element(inline => inline.Child = new FixedElement(20, 10, Colors.Red));
        });

        // 24pt of text plus a 20pt element exceeds 30pt, so the element wraps.
        RecordedPage page = LayoutHarness.Draw(element, new Size(30, 500));
        RectangleOperation block = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == Colors.Red);

        Approximately.Equal(0f, block.Position.X);
        Assert.True(block.Position.Y > 0, "The element should have moved onto the second line.");
    }

    [Fact]
    public void AnInlineElementCanCarryALink()
    {
        TextElement element = new TextElement();
        Container container = new Container();
        container.Child = new FixedElement(20, 10);
        element.Spans.Add(new Text.TextSpan { InlineElement = container, Url = "https://example.com" });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));

        Assert.Single(page.Operations.OfType<ExternalLinkOperation>());
    }

    [Fact]
    public void InlineElementsAreResetBetweenPasses()
    {
        // The paragraph must expose them as children, or their pagination state would survive a new pass.
        SplittableElement splittable = new SplittableElement(unitCount: 2, unitHeight: 10);
        TextElement element = new TextElement();
        Container container = new Container { Child = splittable };
        element.Spans.Add(new Text.TextSpan { InlineElement = container });

        LayoutHarness.Draw(element, new Size(500, 500));
        Assert.Equal(0, splittable.Remaining);

        element.ResetState();

        Assert.Equal(2, splittable.Remaining);
    }

    [Fact]
    public void APlainParagraphIsUnaffected()
    {
        TextElement element = Text(text => text.Span("hello"));

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(30f, plan.Size.Width);
        Approximately.Equal(LineHeight, plan.Size.Height);
    }

    [Fact]
    public void AParagraphKeepsEveryWordWhenAnInlineElementPrecedesAPageBreak()
    {
        // Regression: Draw re-runs BuildLines on every page and re-measures each inline element. An inline
        // element already consumed on page 1 reports Empty on page 2, so its run is dropped, every later line
        // shifts up by one, and _completedLines — an index into the *old* wrapping — skips a line of text.
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(60, 13);
            page.Margin = Edges.All(0);
            page.Content().Text(text =>
            {
                text.Element(inline => inline.Text(nested => nested.Span("IIIIIIIIII")));
                text.Span("aaaaa bbbbb ccccc");
            });
        }));

        List<RecordedPage> pages = LayoutHarness.Render(document).Pages;

        Assert.Equal(["IIIIIIIIII", "aaaaa", "bbbbb", "ccccc"], pages.Select(page => page.Content));
    }

    [Fact]
    public void AnInlineElementTooWideForItsLineIsReportedRatherThanDropped()
    {
        // Regression: the element used to be skipped outright, so the logo vanished from the document and the
        // paragraph still claimed FullRender — content lost with nothing to show for it.
        Element element = LayoutHarness.Build(container => container.Text(text =>
        {
            text.Span("logo:");
            text.Element(inline => inline.Width(200).Height(20));
        }));

        Assert.True(LayoutHarness.Measure(element, new Size(100, 200)).IsWrap);
    }

    [Fact]
    public void AnInlineElementGivenAnExplicitHeightSitsAmongTheWords()
    {
        // Regression: inline elements were measured against Size.Max.Height, so a bounded element nested under
        // one that fills its space reported 14400pt and made the paragraph impossible to place at all.
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(300, 200);
            page.Margin = Edges.All(10);
            page.Content().Text(text =>
            {
                text.Span("An icon ");
                text.Element(inline => inline.Width(10).Height(10).AlignMiddle());
                text.Span(" follows.");
            });
        }));

        Assert.Equal("An icon  follows.", Assert.Single(LayoutHarness.Render(document).Pages).Content);
    }

    [Fact]
    public void AnInlineElementThatFillsItsHeightIsNamedInTheDiagnostic()
    {
        // It cannot be placed — it claims the whole page by definition — but the error used to blame the text
        // height, which sends the reader looking at font sizes instead of at the element.
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(300, 200);
            page.Margin = Edges.All(10);
            page.Content().Text(text =>
            {
                text.Span("An icon ");
                text.Element(inline => inline.AlignMiddle().Width(10).Height(10));
            });
        }));

        DocumentLayoutException exception = Assert.Throws<Pdf.Exceptions.DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Contains("inline", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AlignMiddle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AParagraphBlockedByAnInlineElementDrawsNothing()
    {
        // Measure has reported a wrap. Drawing the lines completed before the blocker would split the paragraph
        // across two pages and then repeat those lines on the next.
        TextElement element = Text(text =>
        {
            text.Line("aaa");
            text.Element(inline => inline.Child = new FixedElement(200, 20));
        });

        Assert.Empty(LayoutHarness.Draw(element, new Size(100, 200)).Operations);
    }

    [Fact]
    public void AnInlineElementCanLinkToASection()
    {
        TextElement element = new TextElement();
        element.Spans.Add(new Text.TextSpan { Text = "ab" });
        element.Spans.Add(new Text.TextSpan { InlineElement = new Container { Child = new FixedElement(20, 10) }, Destination = "intro" });

        RecordedPage page = LayoutHarness.Draw(element, new Size(500, 500));
        InternalLinkOperation link = Assert.Single(page.Operations.OfType<InternalLinkOperation>());

        // The element starts after the two 6pt characters and rests on a baseline its own 10pt height sets.
        Assert.Equal("intro", link.Destination);
        Assert.Equal(new Bounds(12, 0, 32, 10), link.Bounds);
    }

    [Fact]
    public void AnInlineElementOnAContinuationLineIsOfferedTheFullWidth()
    {
        // Only a paragraph's opening line is indented, so an element landing on a later line may use the whole
        // width. The placeholder takes whatever width it is offered, which makes the budget visible.
        TextElement element = new TextElement { FirstLineIndent = 20 };
        element.Spans.Add(new Text.TextSpan { Text = "aaaa bbbb" });
        element.Spans.Add(new Text.TextSpan
        {
            InlineElement = new Container { Child = new ConstrainedElement { MaxHeight = 10, Child = new PlaceholderElement() } }
        });

        RecordedPage page = LayoutHarness.Draw(element, new Size(60, 500));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, block.Position.X);
        Approximately.Equal(60f, block.Size.Width);
    }
}
