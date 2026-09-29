namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Saving how far content has got and returning to it: whatever is drawn after returning is what was drawn after
/// saving, for every kind of content that remembers its progress.
/// </summary>
public class ProgressTests
{
    private static readonly Extent Page = new Extent(200, 40);

    public static TheoryData<string> Kinds => new TheoryData<string>
    {
        "stack", "text", "table", "columns", "columns ending", "flow", "once", "skip first", "new page", "require space",
    };

    private static Block Build(string kind) => kind switch
    {
        "stack" => LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            for (int index = 0; index < 6; index++)
                stack.Add().Height(15).Fill(TestInks.Red).Blank();
        })),
        "text" => LayoutHarness.Build(frame => frame.Text(string.Join(" ", Enumerable.Repeat("word", 60)))),
        "table" => LayoutHarness.Build(frame => frame.Table(table =>
        {
            table.Columns(columns => columns.Share());

            for (int index = 0; index < 6; index++)
                table.Cell().Height(15).Text("row");
        })),
        "columns" => LayoutHarness.Build(frame => frame.Columns(columns =>
        {
            columns.Fixed(20).Fill(TestInks.Red).Blank();
            columns.Share().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 6, unitHeight: 15));
        })),
        "columns ending" => LayoutHarness.Build(frame => frame.Columns(columns =>
        {
            columns.Fixed(20).Fill(TestInks.Red).Blank();
            columns.Share().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 15));
        })),
        "flow" => LayoutHarness.Build(frame => frame.Flow(flow =>
        {
            for (int index = 0; index < 12; index++)
                flow.Add().Width(90).Height(15).Fill(TestInks.Red).Blank();
        })),
        "once" => LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            stack.Add().Once().Height(15).Fill(TestInks.Red).Blank();
            stack.Add().SkipFirst().Height(15).Fill(TestInks.Red).Blank();
            stack.Add().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 15));
        })),
        "skip first" => LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            stack.Add().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 3, unitHeight: 15));
            stack.Add().SkipFirst().Height(15).Fill(TestInks.Red).Blank();
        })),
        "new page" => LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            stack.Add().Height(15).Fill(TestInks.Red).Blank();
            stack.Add().NewPage();
            stack.Add().Height(15).Fill(TestInks.Red).Blank();
        })),
        _ => LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            stack.Add().Height(30).Fill(TestInks.Red).Blank();
            stack.Add().RequireSpace(20).Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 3, unitHeight: 15));
        })),
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ReturningToSavedProgressDrawsTheSameAgain(string kind)
    {
        Block content = Build(kind);
        LayoutHarness.Draw(content, Page);

        Progress saved = content.SaveProgress();
        string ahead = Describe(LayoutHarness.Draw(content, Page));
        string further = Describe(LayoutHarness.Draw(content, Page));

        content.RestoreProgress(saved);

        Assert.Equal(ahead, Describe(LayoutHarness.Draw(content, Page)));
        Assert.Equal(further, Describe(LayoutHarness.Draw(content, Page)));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void SavedProgressCanBeReturnedToAgainAndAgain(string kind)
    {
        // Drawing ahead tries one layout after another from the same point, returning to it after each.
        Block content = Build(kind);
        LayoutHarness.Draw(content, Page);

        Progress saved = content.SaveProgress();
        string ahead = Describe(LayoutHarness.Draw(content, Page));

        content.RestoreProgress(saved);
        LayoutHarness.Draw(content, Page);
        content.RestoreProgress(saved);

        Assert.Equal(ahead, Describe(LayoutHarness.Draw(content, Page)));
    }

    [Fact]
    public void ContentThatRemembersNothingSavesNothing() =>
        Assert.Empty(new FixedBlock(10, 10).SaveProgress().Saved);

    [Fact]
    public void ColumnsKeepTheirOwnCopyOfWhatIsFinished()
    {
        Block content = Build("columns");
        Progress saved = content.SaveProgress();

        LayoutHarness.Draw(content, Page);
        LayoutHarness.Draw(content, Page);
        content.RestoreProgress(saved);

        // Returned to before anything was drawn, the fixed column is drawn again.
        Assert.Contains(LayoutHarness.Draw(content, Page).Operations.OfType<RectangleOperation>(), operation => operation.Ink == TestInks.Red);
    }

    private static string Describe(RecordedPage page) =>
        string.Join("; ", page.Operations.Select(operation => operation.ToString()));
}
