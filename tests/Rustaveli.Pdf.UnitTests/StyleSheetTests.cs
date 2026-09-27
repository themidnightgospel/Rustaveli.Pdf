namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Named styles: type, paragraph and frame styles defined once in a document's style sheet, built on one another, and
/// applied by name.
/// </summary>
public class StyleSheetTests
{
    private static readonly Ink Red = TestInks.Red;

    /// <summary>A one-section document whose styles are defined, then its body composed.</summary>
    private static Document Compose(Action<StyleSheet> define, Action<IFrame> body) => Document.Compose(composition =>
    {
        define(composition.Styles);
        composition.Section(section =>
        {
            section.Trim = new Extent(300, 200);
            body(section.Body());
        });
    });

    private static List<TextOperation> Texts(Document document) =>
        LayoutHarness.Render(document).Pages.SelectMany(page => page.Operations).OfType<TextOperation>().ToList();

    [Fact]
    public void ATypeStyleSetsTheWordsNamedWithIt()
    {
        Document document = Compose(
            styles => styles.DefineType("Large", style => style.WithPointSize(20)),
            body => body.Text(text =>
            {
                text.Run("plain ");
                text.Run("large").Style("Large");
            }));

        List<TextOperation> texts = Texts(document);

        Assert.Equal(12f, texts.Single(text => text.Text == "plain ").Style.PointSize);
        Assert.Equal(20f, texts.Single(text => text.Text == "large").Style.PointSize);
    }

    [Fact]
    public void AStyleBasedOnAnotherAppliesTheOtherFirst()
    {
        Document document = Compose(
            styles => styles
                .DefineType("Heading", style => style.WithPointSize(20).Italic())
                .DefineType("Subheading", style => style.WithPointSize(14), basedOn: "Heading")
                .DefineType("Warning", style => style.WithInk(TestInks.Red), basedOn: "Subheading"),
            body => body.Text(text => text.Run("careful").Style("Warning")));

        TypeStyle style = Assert.Single(Texts(document)).Style;

        Assert.Equal(14f, style.PointSize);
        Assert.True(style.IsItalic);
        Assert.Equal((Ink)Red, style.Ink);
    }

    [Fact]
    public void NamedAndInlineStylesApplyInTheOrderCalled()
    {
        Document document = Compose(
            styles => styles.DefineType("Small", style => style.WithPointSize(8)),
            body => body.Text(text =>
            {
                text.Run("after").Style("Small").PointSize(30);
                text.Run("before").PointSize(30).Style("Small");
            }));

        List<TextOperation> texts = Texts(document);

        Assert.Equal(30f, texts.Single(text => text.Text == "after").Style.PointSize);
        Assert.Equal(8f, texts.Single(text => text.Text == "before").Style.PointSize);
    }

    [Fact]
    public void AParagraphStyleSetsTheBlockOfText()
    {
        Document document = Compose(
            styles => styles
                .DefineParagraph("Base", text => text.DefaultType(style => style.WithPointSize(10)))
                .DefineParagraph("Pulled", text => text.FlushRight(), basedOn: "Base"),
            body => body.Text(text =>
            {
                text.Style("Pulled");
                text.Run("quote");
            }));

        TextOperation quote = Assert.Single(Texts(document));

        Assert.Equal(10f, quote.Style.PointSize);

        // Five characters at half of 10 points each, set against the right edge of the 300-point body.
        Approximately.Equal(275f, quote.Position.X);
    }

    [Fact]
    public void AFrameStyleGivesTheFrameItsSettings()
    {
        Document document = Compose(
            styles => styles
                .DefineFrame("Box", frame => frame.Fill(TestInks.Red))
                .DefineFrame("Card", frame => frame.Inset(10), basedOn: "Box"),
            body => body.Style("Card").Text("inside"));

        RecordedPage page = LayoutHarness.Render(document).Pages[0];

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), rectangle => rectangle.Ink == Red);
        TextOperation text = Assert.Single(page.Operations.OfType<TextOperation>());
        Approximately.Equal(10f, text.Position.X);
    }

    [Fact]
    public void ContentComposedAsPagesAreSetNamesStylesToo()
    {
        Document document = Compose(
            styles => styles.DefineType("Large", style => style.WithPointSize(20)),
            body => body.ComposeLater(later => later.Text(text => text.Run("late").Style("Large"))));

        Assert.Equal(20f, Assert.Single(Texts(document)).Style.PointSize);
    }

    [Theory]
    [InlineData("Missing", "No type style is named 'Missing'.")]
    [InlineData("Orphan", "The type style 'Orphan' is based on 'Gone', and no type style is named that.")]
    [InlineData("Loop", "The type style 'Loop' is based, in the end, on itself.")]
    public void AStyleThatCannotBeFoundIsNamedInTheFailure(string name, string message)
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => Compose(
            styles => styles
                .DefineType("Orphan", style => style, basedOn: "Gone")
                .DefineType("Loop", style => style, basedOn: "Around")
                .DefineType("Around", style => style, basedOn: "Loop"),
            body => body.Text(text => text.Run("x").Style(name))));

        Assert.Equal(message, (exception.InnerException ?? exception).Message);
    }

    [Fact]
    public void ParagraphAndFrameStylesAreLookedUpByTheirOwnKind()
    {
        Assert.Throws<CompositionException>(() => Compose(styles => styles.DefineType("Card", style => style), body => body.Style("Card").Text("x")));
        Assert.Throws<CompositionException>(() => Compose(styles => styles.DefineType("Body", style => style), body => body.Text(text => text.Style("Body"))));
    }

    [Fact]
    public void StylesAreNamedOnlyWhileADocumentIsComposed() =>
        Assert.Throws<CompositionException>(() => LayoutHarness.Build(frame => frame.Style("Card")));

    [Fact]
    public void ALaterDefinitionReplacesAnEarlierOne()
    {
        Document document = Compose(
            styles => styles.DefineType("Size", style => style.WithPointSize(8)).DefineType("Size", style => style.WithPointSize(9)),
            body => body.Text(text => text.Run("x").Style("Size")));

        Assert.Equal(9f, Assert.Single(Texts(document)).Style.PointSize);
    }

    [Fact]
    public void AStyleNeedsANameAndSomethingToDo()
    {
        StyleSheet styles = new StyleSheet();

        Assert.ThrowsAny<ArgumentException>(() => styles.DefineType(" ", style => style));
        Assert.Throws<ArgumentNullException>(() => styles.DefineType("A", null!));
        Assert.Throws<ArgumentNullException>(() => styles.DefineParagraph("A", null!));
        Assert.Throws<ArgumentNullException>(() => styles.DefineFrame("A", null!));
        Assert.Throws<ArgumentException>(() => styles.DefineType("A", style => style, basedOn: " "));
        Assert.Same(styles, styles.DefineType("A", style => style));
    }

    [Fact]
    public void AFrameIsNeededToStyle() =>
        Assert.Throws<ArgumentNullException>(() => ((IFrame)null!).Style("Card"));
}
