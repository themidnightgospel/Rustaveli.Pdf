using System.Text;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// The structure content is drawn in when the output is tagged: what tags it, what is tagged without asking, and what is
/// left out as decoration.
/// </summary>
public class TaggingTests
{
    private static readonly Extent Page = new Extent(300, 400);

    /// <summary>The tree under an element, as <c>Role(Child, Child)</c>.</summary>
    private static string Tree(StructureElement element)
    {
        StringBuilder text = new StringBuilder(element.Role);
        List<StructureElement> children = element.Kids.OfType<StructureElement>().ToList();

        if (children.Count > 0)
            text.Append('(').Append(string.Join(", ", children.Select(Tree))).Append(')');

        return text.ToString();
    }

    /// <summary>Each text drawn, with the role of the element it was drawn in, or null for decoration.</summary>
    private static List<(string? Role, string Text)> Texts(RecordingSurface surface)
    {
        List<(string?, string)> texts = [];
        StructureElement? current = surface.Root;

        foreach (DrawOperation operation in surface.Pages.SelectMany(page => page.Operations))
        {
            if (operation is TagOperation tag)
                current = tag.Element;
            else if (operation is TextOperation text)
                texts.Add((current?.Role, text.Text));
        }

        return texts;
    }

    private static StructureElement Only(StructureElement element) =>
        Assert.IsType<StructureElement>(Assert.Single(element.Kids));

    [Fact]
    public void UntaggedOutputHearsNothingOfTheStructure()
    {
        RecordedPage page = LayoutHarness.Render(frame => frame.Tagged(ContentTag.Heading(1)).Text("Title"), Page);

        Assert.Empty(page.Operations.OfType<TagOperation>());
        Assert.Single(page.Operations.OfType<TextOperation>());
    }

    [Fact]
    public void TaggedContentIsOneElementUnderTheOneItIsDrawnIn()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Section).Stack(stack =>
            {
                stack.Add().Tagged(ContentTag.Heading(2)).Text("Title");
                stack.Add().Tagged(ContentTag.Figure("A square")).Width(20).Height(20).Blank();
            }),
            Page);

        Assert.Equal("Document(Sect(H2, Figure))", Tree(root));
        Assert.Equal([("H2", "Title")], Texts(surface));
        Assert.Equal("A square", Only(root).Kids.OfType<StructureElement>().Last().AlternateText);
    }

    [Fact]
    public void ContentThatGoesOnToAnotherPageStaysOneElement()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Paragraph).Text(text =>
            {
                for (int line = 1; line <= 30; line++)
                    text.Line("Line " + line);
            }),
            new Extent(300, 60),
            pages: 3);

        StructureElement paragraph = Only(root);
        Assert.Equal("P", paragraph.Role);
        Assert.All(Texts(surface), text => Assert.Equal("P", text.Role));
        Assert.Contains(surface.Pages[1].Operations, operation => operation is TextOperation);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void ContentWithNothingToDrawHereCreatesNoElement(string outcome)
    {
        StructureElement root = new StructureElement("Document", null);
        RecordingSurface surface = new RecordingSurface();
        RenderContext context = new RenderContext(surface, LayoutHarness.Context(), root);
        surface.BeginPage(Page);

        new TagBlock { Tag = ContentTag.Paragraph, Child = ScriptedBlock.WithNothingToDraw(outcome) }.Render(Page, context);

        using (context.Tags.Enter(new StructureElement("P", root)))
            new LanguageBlock { Language = "ka", Child = ScriptedBlock.WithNothingToDraw(outcome) }.Render(Page, context);

        Assert.Empty(root.Kids);
    }

    [Fact]
    public void TextNothingElseTagsIsAParagraph()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Stack(stack =>
            {
                stack.Add().Text("One");
                stack.Add().Tagged(ContentTag.Section).Text("Two");
                stack.Add().Tagged(ContentTag.Heading(1)).Text("Three");
                stack.Add().Tagged(ContentTag.Caption).Text("Four");
            }),
            Page);

        Assert.Equal("Document(P, Sect(P), H1, Caption)", Tree(root));
        Assert.Equal([("P", "One"), ("P", "Two"), ("H1", "Three"), ("Caption", "Four")], Texts(surface));
    }

    [Fact]
    public void AParagraphGoingOnToAnotherPageIsStillOne()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(
            frame => frame.Text(text =>
            {
                for (int line = 1; line <= 30; line++)
                    text.Line("Line " + line);
            }),
            new Extent(300, 60),
            pages: 3);

        Assert.Equal("Document(P)", Tree(root));
    }

    [Fact]
    public void UntaggedContentIsDecorationAndCreatesNothing()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Stack(stack =>
            {
                stack.Add().Untagged().Tagged(ContentTag.Heading(1)).Text("Flourish");
                stack.Add().Text("Content");
            }),
            Page);

        Assert.Equal("Document(P)", Tree(root));
        Assert.Equal([(null, "Flourish"), ("P", "Content")], Texts(surface));
    }

    [Fact]
    public void LinkedWordsAreALinkInTheirParagraph()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Text(text =>
            {
                text.Run("See ");
                text.Link("the site", "https://example.com");
                text.Run(" and ");
                text.CrossReference("the notes", "notes");
            }),
            Page);

        Assert.Equal("Document(P(Link, Link))", Tree(root));
        Assert.Equal([("P", "See "), ("Link", "the site"), ("P", " and "), ("Link", "the notes")], Texts(surface));

        List<DrawOperation> operations = surface.Pages[0].Operations;
        int link = operations.FindIndex(operation => operation is ExternalLinkOperation);
        TagOperation before = Assert.IsType<TagOperation>(operations.Take(link).Last(operation => operation is TagOperation));
        Assert.Equal("Link", before.Element!.Role);
    }

    [Fact]
    public void LinkedFramesAreLinks()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Stack(stack =>
            {
                stack.Add().Link("https://example.com").Text("Site");
                stack.Add().CrossReference("notes").Text("Notes");
            }),
            Page);

        Assert.Equal("Document(Link, Link)", Tree(root));
        Assert.Equal([("Link", "Site"), ("Link", "Notes")], Texts(surface));
    }

    [Fact]
    public void ALanguageAmongGroupsIsCarriedByTheElementsInside()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(frame => frame.Language("ka").Text("გამარჯობა"), Page);

        StructureElement paragraph = Only(root);
        Assert.Equal(("P", "ka"), (paragraph.Role, paragraph.Language));
    }

    [Fact]
    public void ALanguageWithinTextIsASpan()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Paragraph).Language("ka").Text("გამარჯობა"),
            Page);

        StructureElement span = Only(Only(root));
        Assert.Equal(("Span", "ka"), (span.Role, span.Language));
        Assert.Equal([("Span", "გამარჯობა")], Texts(surface));
    }

    [Fact]
    public void ASpanGoingOnToAnotherPageIsStillOne()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Paragraph).Language("ka").Text(text =>
            {
                for (int line = 1; line <= 30; line++)
                    text.Line("Line " + line);
            }),
            new Extent(300, 60),
            pages: 3);

        Assert.Equal("Document(P(Span))", Tree(root));
        Assert.All(Texts(surface), text => Assert.Equal("Span", text.Role));
        Assert.Contains(surface.Pages[1].Operations, operation => operation is TextOperation);
    }

    [Fact]
    public void ALanguageInUntaggedOutputOnlyDrawsItsContent()
    {
        RecordedPage page = LayoutHarness.Render(frame => frame.Tagged(ContentTag.Paragraph).Language("ka").Text("გამარჯობა"), Page);

        Assert.Empty(page.Operations.OfType<TagOperation>());
        Assert.Equal("გამარჯობა", Assert.Single(page.Operations.OfType<TextOperation>()).Text);
    }

    [Fact]
    public void ALanguageUntaggedChangesNothing()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(frame => frame.Untagged().Language("ka").Text("გამარჯობა"), Page);

        Assert.Empty(root.Kids);
    }

    [Fact]
    public void AListIsTaggedAsOne()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.List(list =>
            {
                list.Add().Text("First");
                list.Add().Text("Second");
            }),
            Page);

        Assert.Equal("Document(L(LI(Lbl, LBody), LI(Lbl, LBody)))", Tree(root));
        Assert.Equal([("Lbl", "•"), ("LBody", "First"), ("Lbl", "•"), ("LBody", "Second")], Texts(surface));
    }

    private static void Rows(TableComposer table, int rows, bool header = false, bool footer = false)
    {
        table.Columns(columns =>
        {
            columns.Share();
            columns.Share();
        });

        if (header)
        {
            table.HeaderRows(band =>
            {
                band.Cell().Text("Name");
                band.Cell().Text("Value");
            });
        }

        if (footer)
            table.FooterRows(band => band.Cell().SpanColumns(2).Text("Total"));

        for (int row = 1; row <= rows; row++)
        {
            table.Cell().RowHeading().Text("Row " + row);
            table.Cell().Text("Value " + row);
        }
    }

    [Fact]
    public void ATableTaggedAsOneIsTaggedRowByRowAndCellByCell()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Table).Table(table => Rows(table, 2, header: true, footer: true)),
            Page);

        Assert.Equal(
            "Document(Table(THead(TR(TH, TH)), TBody(TR(TH, TD), TR(TH, TD)), TFoot(TR(TD))))",
            Tree(root));

        StructureElement table = Only(root);
        List<StructureElement> cells = table.Kids.OfType<StructureElement>()
            .SelectMany(group => group.Kids.OfType<StructureElement>())
            .SelectMany(row => row.Kids.OfType<StructureElement>())
            .ToList();

        Assert.Equal(
            [TableScope.Column, TableScope.Column, TableScope.Row, null, TableScope.Row, null, null],
            cells.Select(cell => cell.Scope));
        Assert.Equal([1, 1, 1, 1, 1, 1, 2], cells.Select(cell => cell.ColumnSpan));
        Assert.All(cells, cell => Assert.Equal(1, cell.RowSpan));
        Assert.Equal(("TH", "Name"), Texts(surface)[0]);
        Assert.Equal(("TD", "Total"), Texts(surface)[^1]);
    }

    [Fact]
    public void ATableTaggedAsOneWithoutBandsHasABodyAlone()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Table).Table(table =>
            {
                Rows(table, 1);
                table.Cell().SpanRows(2).Text("Tall");
            }),
            Page);

        Assert.Equal("Document(Table(TBody(TR(TH, TD), TR(TD))))", Tree(root));
        StructureElement tall = (StructureElement)((StructureElement)((StructureElement)Only(Only(root)).Kids[1]).Kids[0]);
        Assert.Equal(2, tall.RowSpan);
    }

    [Fact]
    public void RepeatedBandsAreReadOnceAndDecorationAfter()
    {
        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(
            frame => frame.Tagged(ContentTag.Table).Table(table => Rows(table, 30, header: true, footer: true)),
            new Extent(300, 120),
            pages: 12);

        StructureElement table = Only(root);
        List<StructureElement> groups = table.Kids.OfType<StructureElement>().ToList();
        Assert.Equal(["THead", "TBody", "TFoot"], groups.Select(group => group.Role));
        Assert.Single(groups[0].Kids);
        Assert.Equal(30, groups[1].Kids.Count);
        Assert.Single(groups[2].Kids);

        List<(string? Role, string Text)> texts = Texts(surface);
        Assert.Equal(("TH", "Name"), texts.First(text => text.Text == "Name"));
        Assert.Equal((null, "Name"), texts.Last(text => text.Text == "Name"));
        Assert.Equal(("TD", "Total"), texts.First(text => text.Text == "Total"));
        Assert.Equal((null, "Total"), texts.Last(text => text.Text == "Total"));
        Assert.True(surface.Pages.Count(page => page.Operations.OfType<TextOperation>().Any()) > 1);
    }

    [Fact]
    public void ATableNotTaggedAsOneIsLayoutAndItsTextParagraphs()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(frame => frame.Table(table => Rows(table, 1)), Page);

        Assert.Equal("Document(P, P)", Tree(root));
    }

    [Fact]
    public void ATableTaggedButUntaggedIsNotTagged()
    {
        (StructureElement root, _) = LayoutHarness.RenderTagged(
            frame => frame.Untagged().Tagged(ContentTag.Table).Table(table => Rows(table, 1, header: true)),
            Page);

        Assert.Empty(root.Kids);
    }

    [Fact]
    public void TheRunningHeadAndFootPaperUnderlayAndOverlayAreDecoration()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Paper = Ink.Rgb(250, 250, 240);
            section.Underlay().Text("Under");
            section.RunningHead().Text("Head");
            section.Body().Text("Body");
            section.RunningFoot().Text("Foot");
            section.Overlay().Text("Over");
        }));

        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(document);

        Assert.Equal("Document(P)", Tree(root));
        Assert.Equal([(null, "Under"), (null, "Head"), ("P", "Body"), (null, "Foot"), (null, "Over")], Texts(surface));

        RectangleOperation paper = surface.Pages[0].Operations.OfType<RectangleOperation>().First();
        TagOperation before = surface.Pages[0].Operations.TakeWhile(operation => operation != paper).OfType<TagOperation>().Last();
        Assert.Null(before.Element);
    }

    [Fact]
    public void OnlyTheFinalPassIsTagged()
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Text("Body")));

        RecordingSurface untagged = LayoutHarness.Render(document);
        (StructureElement root, RecordingSurface tagged) = LayoutHarness.RenderTagged(document);

        Assert.Empty(untagged.Pages.SelectMany(page => page.Operations).OfType<TagOperation>());
        Assert.Equal("Document(P)", Tree(root));
        Assert.Single(tagged.Pages);
    }

    [Fact]
    public void ContentInADrawOrderIsDrawnInItsElement()
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Stack(stack =>
        {
            stack.Add().DrawOrder(1).Tagged(ContentTag.Heading(1)).Text("Above");
            stack.Add().Text("Below");
        })));

        (StructureElement root, RecordingSurface surface) = LayoutHarness.RenderTagged(document);

        Assert.Equal("Document(H1, P)", Tree(root));
        Assert.Equal([("P", "Below"), ("H1", "Above")], Texts(surface));
    }
}
