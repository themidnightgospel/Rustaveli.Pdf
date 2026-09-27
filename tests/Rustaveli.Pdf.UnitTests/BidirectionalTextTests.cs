namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Right-to-left and mixed text put in display order by the bidirectional algorithm, against
/// <see cref="FakeTypeMeasurer"/>: every character is 6pt wide and every line 12pt tall.
/// </summary>
/// <remarks>
/// A right-to-left piece is drawn as the characters it shows from left to right, so the Hebrew "שלום" (shin, lamed,
/// vav, final mem) is drawn as "םולש".
/// </remarks>
public class BidirectionalTextTests
{
    private const string Shalom = "שלום";
    private const string ShalomDrawn = "םולש";

    private static RecordedPage Draw(Action<TextComposer> compose, ReadingDirection direction = ReadingDirection.LeftToRight, float width = 500)
    {
        TextBlock element = new TextBlock();
        compose(new TextComposer(element));
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = direction;

        return LayoutHarness.Draw(element, new Extent(width, 500), context);
    }

    private static List<string> Drawn(RecordedPage page) => page.Texts.Select(text => text.Text).ToList();

    [Fact]
    public void AHebrewWordIsDrawnRightToLeft()
    {
        RecordedPage page = Draw(text => text.Run(Shalom));

        Assert.Equal([ShalomDrawn], Drawn(page));
        Approximately.Equal(0f, page.Texts.Single().Position.X);
    }

    [Fact]
    public void AHebrewWordAmongEnglishKeepsItsPlaceInTheSentence()
    {
        RecordedPage page = Draw(text => text.Run($"abc {Shalom} def"));

        Assert.Equal([$"abc {ShalomDrawn} def"], Drawn(page));
        Approximately.Equal(0f, page.Texts.Single().Position.X);
    }

    [Fact]
    public void ARightToLeftParagraphSetsItsFirstWordAtTheRight()
    {
        RecordedPage page = Draw(text => text.Run($"{Shalom} abc"), ReadingDirection.RightToLeft, width: 100);

        // The English word comes second in reading, so it sits to the left; the line is flush right.
        Assert.Equal([$"abc {ShalomDrawn}"], Drawn(page));
        Approximately.Equal(52f, page.Texts.First().Position.X);
    }

    [Fact]
    public void RightToLeftWordsFollowOneAnotherLeftward()
    {
        RecordedPage page = Draw(text => text.Run("אב גד"));

        // "אב" (alef, bet) is read first, so it is drawn at the right, with "גד" to its left.
        Assert.Equal(["דג בא"], Drawn(page));
    }

    [Fact]
    public void BracketsInRightToLeftTextAreMirrored()
    {
        RecordedPage page = Draw(text => text.Run($"({Shalom})"), ReadingDirection.RightToLeft);

        // Reversed, the closing bracket would come first; mirrored, it shows as the opening one again.
        Assert.Equal([$"({ShalomDrawn})"], Drawn(page));
    }

    [Fact]
    public void AWordMixingScriptsShowsEachInItsOwnDirection()
    {
        RecordedPage page = Draw(text => text.Run($"abc{Shalom}"));

        Assert.Equal([$"abc{ShalomDrawn}"], Drawn(page));
        Approximately.Equal(0f, page.Texts.Single().Position.X);
    }

    [Fact]
    public void RunsInDifferentTypeStayWithTheirOwnText()
    {
        RecordedPage page = Draw(text =>
        {
            text.Run("אב ");
            text.Run("גד").Bold();
        });

        TextOperation bold = page.Texts.Single(operation => operation.Style.Weight == TypeWeight.Bold);

        // The bold word is read second, so it is drawn first, at the left.
        Assert.Equal("דג", bold.Text);
        Approximately.Equal(0f, bold.Position.X);
    }

    [Fact]
    public void AnInlineFrameAmongRightToLeftWordsMovesWithThem()
    {
        RecordedPage page = Draw(text =>
        {
            text.Run("אב");
            text.Inline(frame => frame.Slot().Child = new FixedBlock(12, 6, TestInks.Red));
            text.Run("גד");
        });

        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Displayed as "דג", the frame, then "בא": the frame sits between them, 12pt in.
        Assert.Equal(["דג", "בא"], Drawn(page));
        Approximately.Equal(12f, block.Position.X);
        Approximately.Equal(24f, page.Texts.Last().Position.X);
    }

    [Fact]
    public void AnEllipsisEndsARightToLeftLineAtItsLeft()
    {
        RecordedPage page = Draw(
            text =>
            {
                text.MaxLines(1);
                text.Run($"{Shalom} {Shalom} {Shalom}");
            },
            ReadingDirection.RightToLeft,
            width: 40);

        Assert.Equal([$"…{ShalomDrawn}"], Drawn(page));
    }

    [Fact]
    public void EachParagraphIsResolvedOnItsOwn()
    {
        // Hebrew ending one paragraph has no hold on the next: the English after the break is set as it reads.
        RecordedPage page = Draw(text => text.Run($"{Shalom}\nabc def"));

        Assert.Equal([ShalomDrawn, "abc def"], Drawn(page));
        Approximately.Equal(0f, page.Texts.Single(operation => operation.Text == "abc def").Position.X);
    }

    [Fact]
    public void ARunSetRightToLeftEndsAtItsLeft()
    {
        RecordedPage page = Draw(text =>
        {
            text.Run("abc ");
            text.Run("def ghi!").RightToLeft();
        });

        // Latin words keep their order even so — the space between two of them reads left to right — but the "!"
        // ending the run ends it on the left, where a right-to-left run ends.
        Assert.Equal(["abc ", "!def ghi"], Drawn(page));
    }

    [Fact]
    public void ARunSetLeftToRightKeepsItsPunctuationAtItsOwnEnd()
    {
        // Unset, the "!" ending a right-to-left paragraph takes the paragraph's direction and moves to the phrase's
        // left; set apart as left to right, the phrase keeps it.
        RecordedPage unset = Draw(text => text.Run($"{Shalom} abc!"), ReadingDirection.RightToLeft);
        RecordedPage isolated = Draw(
            text =>
            {
                text.Run($"{Shalom} ");
                text.Run("abc!").LeftToRight();
            },
            ReadingDirection.RightToLeft);

        Assert.Equal([$"!abc {ShalomDrawn}"], Drawn(unset));
        Assert.Equal(["abc!", $" {ShalomDrawn}"], Drawn(isolated));
    }

    [Fact]
    public void ARunSetRightToLeftStaysSoAcrossALineBreak()
    {
        RecordedPage page = Draw(text => text.Run("ab!\ncd!").RightToLeft());

        Assert.Equal(["!ab", "!cd"], Drawn(page));
    }

    [Fact]
    public void RunsWithoutADirectionOfTheirOwnAreNotReordered()
    {
        RecordedPage page = Draw(text =>
        {
            text.Run("ab ");
            text.Run("cd").LeftToRight();
        });

        Assert.Equal(["ab ", "cd"], Drawn(page));
    }

    [Fact]
    public void AJustifiedRightToLeftLineStretchesBetweenItsWords()
    {
        RecordedPage page = Draw(
            text =>
            {
                text.Justified();
                text.Run("אבג דהו זחט יכל");
            },
            ReadingDirection.RightToLeft,
            width: 80);

        // The first line, "אבג דהו זחט" (66pt), fills 80pt: its first word at the right edge, its last at the left.
        Approximately.Equal(0f, page.Texts.First().Position.X);
        Approximately.Equal(62f, page.Texts.Single(operation => operation.Text == "גבא").Position.X);
    }
}
