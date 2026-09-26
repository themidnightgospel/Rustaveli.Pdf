namespace Rustaveli.Pdf.UnitTests;

public class MirrorTests
{
    [Fact]
    public void MirroringDoesNotChangeTheReportedSize()
    {
        MirrorBlock element = new MirrorBlock { Horizontally = true, Child = new FixedBlock(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void MirroringHorizontallyReflectsContentBackOverItsOwnBox()
    {
        MirrorBlock element = new MirrorBlock { Horizontally = true, Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Reflected about the box's right edge, the origin lands where the far corner was.
        Approximately.Equal(50f, drawn.Position.X);
    }

    [Fact]
    public void MirroringVerticallyReflectsDownwards()
    {
        MirrorBlock element = new MirrorBlock { Vertically = true, Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(20f, drawn.Position.Y);
    }

    [Fact]
    public void DrawsNothingWithoutContent()
    {
        MirrorBlock element = new MirrorBlock { Horizontally = true, Vertically = true };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void DoesNotAskAChildWithNothingToShowToDraw(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        MirrorBlock element = new MirrorBlock { Horizontally = true, Child = child };

        LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(child.DrawnWith);
    }
}
