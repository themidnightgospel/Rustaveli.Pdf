namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Paragraph composition against <see cref="FakeTypeMeasurer"/>: characters are 6pt wide and lines 12pt tall
/// at the default size.
/// </summary>
public class TextComposerTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static RecordedPage Draw(Action<TextComposer> compose, Pagination? page = null) =>
        LayoutHarness.Draw(
            LayoutHarness.Build(container => container.Text(compose)),
            Space,
            LayoutHarness.Context(page));

    [Fact]
    public void CrossReferenceMakesTheRunJumpToTheAnchor()
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
        context.RegisterAnchor("summary", 4);

        Assert.Equal("4", Draw(text => text.FolioOf("summary"), context).Content);
    }

    [Fact]
    public void PageNumberOfSectionShowsAPlaceholderUntilTheSectionIsReached() =>
        Assert.Equal("?", Draw(text => text.FolioOf("summary")).Content);

    [Fact]
    public void TotalPagesShowsTheDocumentTotal() =>
        Assert.Equal("9", Draw(text => text.PageCount(), new Pagination { PageCount = 9 }).Content);

    [Fact]
    public void FlushLeftOverridesTheRightToLeftDefault()
    {
        Block aligned = LayoutHarness.Build(container => container.RightToLeft().Text(text =>
        {
            text.FlushLeft();
            text.Run("Hello");
        }));
        Block unaligned = LayoutHarness.Build(container => container.RightToLeft().Text("Hello"));

        // Right-to-left text hugs the right edge unless told otherwise: 200 less five 6pt characters.
        Approximately.Equal(0f, Assert.Single(LayoutHarness.Draw(aligned, Space).Texts).Position.X);
        Approximately.Equal(170f, Assert.Single(LayoutHarness.Draw(unaligned, Space).Texts).Position.X);
    }

    [Theory]
    [InlineData("left", false, 0f)]
    [InlineData("left", true, 0f)]
    [InlineData("centre", false, 85f)]
    [InlineData("right", false, 170f)]
    [InlineData("right", true, 170f)]
    [InlineData("start", false, 0f)]
    [InlineData("start", true, 170f)]
    [InlineData("end", false, 170f)]
    [InlineData("end", true, 0f)]
    [InlineData("justified", false, 0f)]
    [InlineData("justified", true, 170f)]
    public void EachAlignmentSetsTheLineWhereItSays(string alignment, bool rightToLeft, float expectedX)
    {
        Block root = LayoutHarness.Build(container => container.Reading(rightToLeft ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight).Text(text =>
        {
            // Set against the grain first, so each call is seen to replace what came before.
            text.FlushEnd();

            switch (alignment)
            {
                case "left": text.FlushLeft(); break;
                case "centre": text.Centered(); break;
                case "right": text.FlushRight(); break;
                case "start": text.FlushStart(); break;
                case "end": text.FlushLeft(); text.FlushEnd(); break;
                default: text.Justified(); break;
            }

            text.Run("Hello");
        }));

        Approximately.Equal(expectedX, Assert.Single(LayoutHarness.Draw(root, Space).Texts).Position.X);
    }

    [Fact]
    public void DefaultTypeReachesEveryRun()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.WithPointSize(20));
            text.Run("a");
            text.Run(" b");
        });

        Assert.Equal("a b", page.Content);
        Assert.All(page.Texts, run => Approximately.Equal(20f, run.Style.PointSize));
    }

    [Fact]
    public void DefaultTypesCompose()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.WithPointSize(20));
            text.DefaultType(style => style.Bold());
            text.Run("a");
        });

        TypeStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(20f, style.PointSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void ALaterDefaultTypeOverridesAnEarlierOne()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.WithPointSize(10));
            text.DefaultType(style => style.WithPointSize(20));
            text.Run("a");
        });

        Approximately.Equal(20f, Assert.Single(page.Texts).Style.PointSize);
    }

    [Fact]
    public void ASpanRefinesTheParagraphDefault()
    {
        RecordedPage page = Draw(text =>
        {
            text.DefaultType(style => style.WithPointSize(20));
            text.Run("a").Bold();
        });

        TypeStyle style = Assert.Single(page.Texts).Style;

        Approximately.Equal(20f, style.PointSize);
        Assert.Equal(TypeWeight.Bold, style.Weight);
    }

    [Fact]
    public void AnInlineFrameLeftEmptyAddsNothingToTheParagraph()
    {
        TextBlock element = new TextBlock();
        TextComposer text = new TextComposer(element);

        text.Run("ab");
        text.Inline(_ => { });
        text.Run("cd");

        // A paragraph holding an inline frame has its lines rebuilt on every pass; an empty one must not cost that.
        Assert.DoesNotContain(element.Runs, run => run.Inline is not null);
        Assert.Equal("abcd", Assert.Single(LayoutHarness.Draw(element, Space).Texts).Text);
    }

    [Fact]
    public void ComposeRefusesAMissingHandler()
    {
        TextBlock element = new TextBlock();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new TextComposer(element).Inline(null!));

        Assert.Equal("handler", exception.ParamName);
        Assert.Empty(element.Runs);
    }
}
