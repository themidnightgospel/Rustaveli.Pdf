namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Content composed only when layout reaches it, and let go or kept once drawn.
/// </summary>
public class ComposeLaterTests
{
    private static readonly Extent Page = new Extent(100, 60);

    private static (LaterBlock Block, Func<int> Composed) Later(bool keep, int units = 4)
    {
        int composed = 0;
        LaterBlock block = new LaterBlock
        {
            Keep = keep,
            Compose = frame =>
            {
                composed++;
                frame.Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: units, unitHeight: 30));
            },
        };

        return (block, () => composed);
    }

    [Fact]
    public void NothingIsComposedUntilLayoutReachesIt()
    {
        (LaterBlock block, Func<int> composed) = Later(keep: false);

        Assert.Equal(0, composed());
        Assert.Empty(block.GetChildren().OfType<Block>());

        LayoutHarness.Measure(block, Page);

        Assert.Equal(1, composed());
    }

    [Fact]
    public void TheContentFlowsAcrossPagesAndIsLetGoOnceDrawn()
    {
        (LaterBlock block, Func<int> composed) = Later(keep: false);

        Assert.True(LayoutHarness.Measure(block, Page).IsPartial);
        Assert.Equal(2, LayoutHarness.Draw(block, Page).Operations.Count);
        Assert.True(LayoutHarness.Measure(block, Page).IsComplete);
        Assert.Equal(2, LayoutHarness.Draw(block, Page).Operations.Count);

        Assert.Equal(1, composed());
        Assert.Empty(block.GetChildren().OfType<Block>());
        Assert.True(LayoutHarness.Measure(block, Page).IsNothing);
        Assert.Empty(LayoutHarness.Draw(block, Page).Operations);
    }

    [Fact]
    public void LetGoContentIsComposedAfreshInTheNextPass()
    {
        (LaterBlock block, Func<int> composed) = Later(keep: false, units: 1);

        LayoutHarness.Draw(block, Page);
        block.ResetState();
        LayoutHarness.Draw(block, Page);

        Assert.Equal(2, composed());
    }

    [Fact]
    public void KeptContentIsComposedOnceForEveryPass()
    {
        (LaterBlock block, Func<int> composed) = Later(keep: true, units: 1);

        LayoutHarness.Draw(block, Page);
        Assert.Single(block.GetChildren().OfType<Block>());

        block.ResetState();
        Assert.Single(LayoutHarness.Draw(block, Page).Operations);

        Assert.Equal(1, composed());
    }

    [Fact]
    public void ContentThatDoesNotFitIsNotDrawnYet()
    {
        (LaterBlock block, _) = Later(keep: false);

        Assert.Empty(LayoutHarness.Draw(block, new Extent(100, 20)).Operations);
        Assert.True(LayoutHarness.Measure(block, Page).IsPartial);
    }

    [Fact]
    public void ProgressIsSavedWithTheContentHeld()
    {
        (LaterBlock block, _) = Later(keep: false);
        LayoutHarness.Draw(block, Page);

        Progress saved = block.SaveProgress();
        LayoutHarness.Draw(block, Page);
        block.RestoreProgress(saved);

        Assert.Equal(2, LayoutHarness.Draw(block, Page).Operations.Count);
    }

    [Fact]
    public void ADocumentComposesItsContentLater()
    {
        int composed = 0;
        List<RecordedPage> pages = LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = Page;
            section.Body().ComposeLater(frame =>
            {
                composed++;
                frame.Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 5, unitHeight: 30));
            });
        }))).Pages;

        Assert.Equal(3, pages.Count);
        Assert.True(composed >= 1);
    }

    [Fact]
    public void ComposingLaterNeedsSomethingToCompose() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.ComposeLater(null!)));
}
