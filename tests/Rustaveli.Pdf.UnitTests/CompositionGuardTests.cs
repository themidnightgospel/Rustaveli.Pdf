using Rustaveli.Pdf.Exceptions;

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

        Assert.Null(container.Child);
    }

    [Fact]
    public void CornerRadiusRejectsASingleSidedBorder()
    {
        // The element cannot round a corner where two thicknesses meet, so it would have ignored the radius.
        Assert.Throws<InvalidOperationException>(() =>
            LayoutHarness.Build(container => container.StrokeLeft(2).RoundCorners(8)));
    }

    [Fact]
    public void CornerRadiusRejectsAZeroWidthBorder()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LayoutHarness.Build(container => container.Stroke(0).RoundCorners(8)));
    }

    [Fact]
    public void CornerRadiusAcceptsAUniformBorder()
    {
        Block root = LayoutHarness.Build(container => container
            .Stroke(2).RoundCorners(8)
            .Compose(inner => inner.Child = new FixedBlock(40, 20, TestInks.White)));

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
}
