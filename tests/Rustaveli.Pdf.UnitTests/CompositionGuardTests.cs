
namespace Rustaveli.Pdf.UnitTests;

public class CompositionGuardTests
{
    [Fact]
    public void EmptyRefusesToDiscardExistingContent()
    {
        // Empty declares that nothing was placed here. Letting it blank a filled container would destroy a
        // subtree with no diagnostic — exactly what the attach guard exists to prevent.
        Frame container = new Frame();
        container.Text("already here");

        Assert.Throws<CompositionException>(() => container.Blank());
    }

    [Fact]
    public void EmptyIsFineOnAnUntouchedContainer()
    {
        Frame container = new Frame();

        container.Blank();

        Assert.Null(container.Slot().Child);
    }

    [Fact]
    public void RefusesContentForAFrameTheLibraryDidNotMake()
    {
        // IFrame is public so frames can be passed around; one implemented elsewhere has nowhere to hold content.
        CompositionException exception = Assert.Throws<CompositionException>(() => new ForeignFrame().Text("words"));

        Assert.Contains(nameof(ForeignFrame), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ISnippet), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToBlankAFrameTheLibraryDidNotMake()
    {
        Assert.Throws<CompositionException>(() => new ForeignFrame().Blank());
    }

    [Fact]
    public void CornerRadiusRejectsASingleSidedBorder()
    {
        // The element cannot round a corner where two thicknesses meet, so it would have ignored the radius.
        Assert.Throws<CompositionException>(() =>
            LayoutHarness.Build(container => container.StrokeLeft(2).RoundCorners(8)));
    }

    [Fact]
    public void CornerRadiusRejectsAZeroWidthBorder()
    {
        Assert.Throws<CompositionException>(() =>
            LayoutHarness.Build(container => container.Stroke(0).RoundCorners(8)));
    }

    [Fact]
    public void CornerRadiusAcceptsAUniformBorder()
    {
        Block root = LayoutHarness.Build(container => container
            .Stroke(2).RoundCorners(8)
            .Compose(inner => inner.Slot().Child = new FixedBlock(40, 20, TestInks.White)));

        Assert.Single(LayoutHarness.Draw(root, new Extent(200, 200)).Operations.OfType<RoundedRectangleOperation>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkTargetsMustBeMeaningful(string? target)
    {
        // An empty target draws no annotation, so the region would look linked in the source and do nothing.
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(c => c.Link(target!)));
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(c => c.Anchor(target!)));
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(c => c.CrossReference(target!)));
    }

    private sealed class ForeignFrame : IFrame
    {
    }
}
