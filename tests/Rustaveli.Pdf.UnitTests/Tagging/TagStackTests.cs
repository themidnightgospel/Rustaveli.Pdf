using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.Tagging;

/// <summary>
/// The structure elements a render pass draws inside, and what the surface is told of them.
/// </summary>
public class TagStackTests
{
    private static (TagStack Stack, StructureElement Root, RecordingSurface Surface) Tagged()
    {
        RecordingSurface surface = new RecordingSurface();
        StructureElement root = new StructureElement("Document", null);
        return (new TagStack(surface, root), root, surface);
    }

    private static List<StructureElement?> Told(RecordingSurface surface) =>
        surface.Pages.SelectMany(page => page.Operations).OfType<TagOperation>().Select(operation => operation.Element).ToList();

    [Fact]
    public void WithoutARootNothingIsCreatedAndTheSurfaceIsToldNothing()
    {
        RecordingSurface surface = new RecordingSurface();
        surface.BeginPage(new Extent(10, 10));
        TagStack stack = new TagStack(surface, null);

        Assert.False(stack.Enabled);
        Assert.Null(stack.Create("P"));
        Assert.Null(stack.Create(ContentTag.Paragraph));

        using (stack.Untag())
        using (stack.Speak("ka"))
        using (stack.Enter(new StructureElement("P", null)))
        {
            Assert.False(stack.IsUntagged);
        }

        Assert.Empty(Told(surface));
        Assert.Null(surface.CurrentTag);
    }

    [Fact]
    public void WithARootTheSurfaceDrawsInItFromTheStart()
    {
        (TagStack stack, StructureElement root, RecordingSurface surface) = Tagged();

        Assert.True(stack.Enabled);
        Assert.Same(root, stack.Current);
        Assert.Same(root, surface.CurrentTag);
        Assert.False(stack.IsUntagged);
    }

    [Fact]
    public void AnElementIsCreatedAsTheLastChildOfTheCurrentOneWithoutEnteringIt()
    {
        (TagStack stack, StructureElement root, _) = Tagged();

        StructureElement first = stack.Create("P")!;
        StructureElement second = stack.Create("H1")!;

        Assert.Equal([first, second], root.Kids);
        Assert.Same(root, first.Parent);
        Assert.Equal("H1", second.Role);
        Assert.Same(root, stack.Current);
        Assert.Null(first.Language);
    }

    [Fact]
    public void ATagCarriesItsAlternateTextAndExpansion()
    {
        (TagStack stack, _, _) = Tagged();

        StructureElement figure = stack.Create(ContentTag.Figure("A chart"))!;
        StructureElement abbreviation = stack.Create(ContentTag.Abbreviation("for example"))!;

        Assert.Equal(("Figure", "A chart", null), (figure.Role, figure.AlternateText, figure.Expansion));
        Assert.Equal(("Span", null, "for example"), (abbreviation.Role, abbreviation.AlternateText, abbreviation.Expansion));
    }

    [Fact]
    public void EnteringAnElementDrawsInsideItUntilTheScopeEnds()
    {
        (TagStack stack, StructureElement root, RecordingSurface surface) = Tagged();
        surface.BeginPage(new Extent(10, 10));
        StructureElement paragraph = stack.Create("P")!;

        using (stack.Enter(paragraph))
        {
            Assert.Same(paragraph, stack.Current);
            Assert.Same(paragraph, surface.CurrentTag);

            StructureElement span = stack.Create("Span")!;
            Assert.Same(paragraph, span.Parent);
            Assert.Equal([span], paragraph.Kids);
        }

        Assert.Same(root, stack.Current);
        Assert.Equal([paragraph, root], Told(surface));
    }

    [Fact]
    public void EnteringNoElementChangesNothing()
    {
        (TagStack stack, StructureElement root, RecordingSurface surface) = Tagged();
        surface.BeginPage(new Extent(10, 10));

        using (stack.Enter(null))
            Assert.Same(root, stack.Current);

        Assert.Empty(Told(surface));
    }

    [Fact]
    public void UntaggedContentIsDrawnOutsideTheStructureAndCreatesNothing()
    {
        (TagStack stack, StructureElement root, RecordingSurface surface) = Tagged();
        surface.BeginPage(new Extent(10, 10));
        StructureElement paragraph = stack.Create("P")!;

        using (stack.Untag())
        {
            Assert.True(stack.IsUntagged);
            Assert.Null(surface.CurrentTag);
            Assert.Null(stack.Create("H1"));

            using (stack.Untag())
                Assert.True(stack.IsUntagged);

            Assert.True(stack.IsUntagged);

            // An element entered while untagged is still drawn outside the structure.
            using (stack.Enter(paragraph))
                Assert.Null(surface.CurrentTag);

            Assert.Null(surface.CurrentTag);
        }

        Assert.False(stack.IsUntagged);
        Assert.Same(root, surface.CurrentTag);
        Assert.Equal([paragraph], root.Kids);
    }

    [Fact]
    public void ElementsCreatedInALanguageCarryItWhereItDiffersFromTheirParents()
    {
        (TagStack stack, StructureElement root, _) = Tagged();
        root.Language = "en";

        StructureElement plain = stack.Create("P")!;
        StructureElement georgian;
        StructureElement same;

        using (stack.Speak("ka"))
        {
            georgian = stack.Create("P")!;

            using (stack.Enter(georgian))
                same = stack.Create("Span")!;
        }

        using (stack.Speak("en"))
            Assert.Null(stack.Create("P")!.Language);

        StructureElement after = stack.Create("P")!;

        Assert.Null(plain.Language);
        Assert.Equal("ka", georgian.Language);
        Assert.Null(same.Language);
        Assert.Equal("ka", same.EffectiveLanguage);
        Assert.Null(after.Language);
    }

    [Fact]
    public void ALanguageScopeRestoresTheElementInForce()
    {
        (TagStack stack, StructureElement root, RecordingSurface surface) = Tagged();
        StructureElement paragraph = stack.Create("P")!;

        using (stack.Enter(paragraph))
        {
            using (stack.Speak("ka"))
                Assert.Same(paragraph, stack.Current);

            Assert.Same(paragraph, surface.CurrentTag);
        }

        Assert.Same(root, stack.Current);
    }
}
