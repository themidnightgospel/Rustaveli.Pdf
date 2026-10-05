namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Lines built at one width reused at another, where every fit test their building took answers the same: always the
/// lines a fresh paragraph would build there. Every reuse in the test suites is also rebuilt and compared bit for bit
/// (<see cref="TextBlock.VerifiesReuse"/>).
/// </summary>
public class LineReuseTests
{
    private static TextBlock Text(Action<TextComposer> compose)
    {
        TextBlock block = new TextBlock();
        compose(new TextComposer(block));
        return block;
    }

    public static TheoryData<string, Action<TextComposer>> Paragraphs => new()
    {
        {
            "flush end, a line then a word", text =>
            {
                text.FlushEnd();
                text.Line("Hello ");
                text.Run("World");
            }
        },
        { "a label, a break and an amount", text => text.Run("Total: \n42") },
        {
            "spaces and tabs across runs", text =>
            {
                text.Run("words  ");
                text.Run(" \tmore words  here");
                text.Run("  end");
            }
        },
        {
            "a linked trailing space", text =>
            {
                text.Run("linked words ");
                text.Link("ending in a space ", "https://example.com");
                text.Run("after");
            }
        },
        {
            "a blank line, then a space", text =>
            {
                text.Run("\n");
                text.Run(" ");
            }
        },
        { "soft hyphens", text => text.Run("Ty­po­graph­i­cal hy­phen­ation of long words") },
        {
            "a first-line indent and breaks", text =>
            {
                text.FirstLineIndent(12);
                text.Run("Indented words that wrap\nand a second paragraph that wraps too\n\nand a third");
            }
        },
        {
            "justified", text =>
            {
                text.Justified();
                text.Run("Justified words that stretch across several lines of text");
            }
        },
        {
            "centred", text =>
            {
                text.Centered();
                text.Run("Centred words that wrap across lines");
            }
        },
        {
            "a line limit", text =>
            {
                text.MaxLines(2);
                text.Run("A limited paragraph whose later lines are cut away");
            }
        },
        { "type that breaks anywhere", text => text.Run("urn:uuid:6e8bc430-9c3a-11d9-9669").BreakAnywhere() },
        { "a word longer than the line", text => text.Run("Supercalifragilisticexpialidocious and more words") },
        {
            "mixed sizes", text =>
            {
                text.Run("Small words ");
                text.Run("and larger ones").PointSize(20);
                text.Run(" mixed in a line");
            }
        },
    };

    [Theory]
    [MemberData(nameof(Paragraphs))]
    public void PlanningOneParagraphAtManyWidthsGivesWhatAFreshParagraphGives(string name, Action<TextComposer> compose)
    {
        PlanContext context = new PlanContext(new FakeTypeMeasurer(), new Pagination());
        TextBlock reused = Text(compose);

        // Every quarter point from 0 to 520, out of order, so that both kept widths and their ranges are tried.
        for (int step = 0; step < 2081; step++)
        {
            float width = (step * 977 % 2081) * 0.25f;
            Extent room = new Extent(width, 1_000);
            Fit fresh = Text(compose).Plan(room, context);
            Fit planned = reused.Plan(room, context);

            Assert.True(
                fresh.Size.Equals(planned.Size) && fresh.IsComplete == planned.IsComplete && fresh.IsNothing == planned.IsNothing,
                $"{name}, at {width}: planned {planned.Size}, a fresh paragraph {fresh.Size}.");
        }
    }

    [Fact]
    public void LinesAreReusedAtWidthsThatBreakThemTheSame()
    {
        // Characters are 6 points wide at size 12. At 200 points the first line ends after "they", at 198, and the
        // space after it, which would reach 204, does not fit: any width from 198 to just under 204 breaks the same.
        PlanContext context = new PlanContext(new FakeTypeMeasurer(), new Pagination());
        TextBlock block = Text(text => text.Run("Words that wrap at the width they are set at"));
        Fit planned = block.Plan(new Extent(200, 500), context);
        int reused = TextBlock.ReusedAcrossWidths;

        foreach (float width in new[] { 199f, 203.5f, 198.5f, 201f })
            Assert.Equal(planned.Size, block.Plan(new Extent(width, 500), context).Size);

        Assert.Equal(reused + 4, TextBlock.ReusedAcrossWidths);
    }

    [Fact]
    public void AWidthThatBreaksALineDifferentlyBuildsItsOwnLines()
    {
        PlanContext context = new PlanContext(new FakeTypeMeasurer(), new Pagination());
        TextBlock block = Text(text => text.Run("Words that wrap at the width they are set at"));
        block.Plan(new Extent(200, 500), context);
        int reused = TextBlock.ReusedAcrossWidths;

        // At 204 the space after "they" fits, and the line goes on.
        Fit wider = block.Plan(new Extent(204, 500), context);

        Assert.Equal(reused, TextBlock.ReusedAcrossWidths);
        Assert.Equal(Text(text => text.Run("Words that wrap at the width they are set at")).Plan(new Extent(204, 500), context).Size, wider.Size);
    }
}
