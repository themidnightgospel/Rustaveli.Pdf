namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Bookmarks added where their content starts, once.
/// </summary>
public class BookmarkTests
{
    [Fact]
    public void ABookmarkIsAddedWhereItsContentStartsAndOnlyThere()
    {
        List<RecordedPage> pages = LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(100, 60);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(20).Blank();
                stack.Add().Bookmark("Chapter", 2).Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 3, unitHeight: 30));
            });
        }))).Pages;

        BookmarkOperation bookmark = Assert.Single(pages.SelectMany(page => page.Operations).OfType<BookmarkOperation>());
        Assert.Equal(("Chapter", 2, new Offset(0, 20)), (bookmark.Title, bookmark.Level, bookmark.Position));
        Assert.Contains(bookmark, pages[0].Operations);
    }

    [Fact]
    public void ContentThatDoesNotFitYetIsNotBookmarkedYet()
    {
        BookmarkBlock block = new BookmarkBlock { Title = "Late", Level = 1, Child = new FixedBlock(10, 50) };

        Assert.Empty(LayoutHarness.Draw(block, new Extent(10, 20)).Operations);
        Assert.Single(LayoutHarness.Draw(block, new Extent(10, 60)).Operations.OfType<BookmarkOperation>());
    }

    [Fact]
    public void ReturningToSavedProgressBookmarksAgain()
    {
        BookmarkBlock block = new BookmarkBlock { Title = "Again", Level = 1, Child = new FixedBlock(10, 10) };
        Progress saved = block.SaveProgress();

        LayoutHarness.Draw(block, new Extent(10, 20));
        block.RestoreProgress(saved);

        Assert.Single(LayoutHarness.Draw(block, new Extent(10, 20)).Operations.OfType<BookmarkOperation>());
        Assert.Empty(LayoutHarness.Draw(block, new Extent(10, 20)).Operations.OfType<BookmarkOperation>());
    }

    [Fact]
    public void ABookmarkIsHeldWithTheContentOfItsDrawOrder()
    {
        List<DrawOperation> operations = Assert.Single(LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(100, 100);
            section.Body().Stack(stack =>
            {
                stack.Add().DrawOrder(1).Bookmark("Held").Height(10).Blank();
                stack.Add().Height(10).Fill(TestInks.Red).Blank();
            });
        }))).Pages).Operations;

        Assert.True(operations.FindIndex(operation => operation is BookmarkOperation) > operations.FindIndex(operation => operation is RectangleOperation { Ink: var ink } && ink == TestInks.Red));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(" ", 1)]
    [InlineData("Title", 0)]
    public void ABookmarkHasATitleAndALevel(string? title, int level) =>
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(frame => frame.Bookmark(title!, level)));
}
