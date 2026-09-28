namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// A frame fitted to its content, drawn in a box of 100 by 50 around content of 30 by 10: the fill behind it hugs
/// the content on the axes fitted.
/// </summary>
public class FitToContentTests
{
    private static readonly Extent Box = new Extent(100, 50);

    private static RectangleOperation Fill(Func<IFrame, IFrame> fit, ReadingDirection direction = ReadingDirection.LeftToRight)
    {
        Block root = LayoutHarness.Build(frame => fit(frame.Reading(direction)).Fill(TestInks.Red).Compose(inner =>
            inner.Slot().Child = new FixedBlock(30, 10)));

        return LayoutHarness.Draw(root, Box).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red);
    }

    [Fact]
    public void WithoutFittingTheFillCoversTheWholeBox() =>
        Assert.Equal(new Extent(100, 50), Fill(frame => frame).Size);

    [Fact]
    public void FittedBothWaysTheFillHugsTheContent()
    {
        RectangleOperation fill = Fill(frame => frame.FitToContent());

        Assert.Equal(new Extent(30, 10), fill.Size);
        Assert.Equal(Offset.Zero, fill.Position);
    }

    [Fact]
    public void FittedAcrossTheFillKeepsTheHeight() =>
        Assert.Equal(new Extent(30, 50), Fill(frame => frame.FitWidthToContent()).Size);

    [Fact]
    public void FittedDownTheFillKeepsTheWidth() =>
        Assert.Equal(new Extent(100, 10), Fill(frame => frame.FitHeightToContent()).Size);

    [Fact]
    public void RightToLeftTheContentStartsAtTheRight()
    {
        RectangleOperation fill = Fill(frame => frame.FitToContent(), ReadingDirection.RightToLeft);

        Assert.Equal(new Offset(70, 0), fill.Position);
    }

    [Fact]
    public void FittedOnlyDownTheContentStaysAcrossTheWholeWidthRightToLeft() =>
        Assert.Equal(Offset.Zero, Fill(frame => frame.FitHeightToContent(), ReadingDirection.RightToLeft).Position);

    [Fact]
    public void FittingLeavesTheReportedSizeAlone()
    {
        Block root = LayoutHarness.Build(frame => frame.FitToContent().Compose(inner => inner.Slot().Child = new FixedBlock(30, 10)));

        Assert.Equal(new Extent(30, 10), LayoutHarness.Measure(root, Box).Size);
    }

    [Fact]
    public void ContentThatDoesNotFitIsNotDrawn()
    {
        FitToContentBlock element = new FitToContentBlock { Child = new FixedBlock(300, 10, TestInks.Red) };

        Assert.Empty(LayoutHarness.Draw(element, Box).Operations);
        Assert.Empty(LayoutHarness.Draw(new FitToContentBlock(), Box).Operations);
    }
}
