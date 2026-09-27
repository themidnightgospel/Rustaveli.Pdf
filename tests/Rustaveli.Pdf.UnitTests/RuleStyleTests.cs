namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Rules drawn solid, in a stroke style, or in a pattern of dashes, across a box 100 wide and 30 tall.
/// </summary>
public class RuleStyleTests
{
    private static readonly Extent Space = new Extent(100, 30);

    [Fact]
    public void ASolidRuleIsABar()
    {
        RecordedPage page = LayoutHarness.Draw(frame => frame.Rule(2, TestInks.Red), Space);

        RectangleOperation bar = Assert.Single(page.Operations.OfType<RectangleOperation>());
        Assert.Equal(new Extent(100, 2), bar.Size);
        Assert.Empty(page.Operations.OfType<LineOperation>());
    }

    [Theory]
    [InlineData(StrokeStyle.Dashed, 2f)]
    [InlineData(StrokeStyle.Dotted, 2f)]
    [InlineData(StrokeStyle.Double, 2f)]
    [InlineData(StrokeStyle.Wavy, 6f)]
    public void AStyledRuleIsStrokedAlongItsCentre(StrokeStyle style, float breadth)
    {
        Block root = LayoutHarness.Build(frame => frame.Rule(2, TestInks.Red, style));

        LineOperation line = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>());

        Approximately.Equal(new Extent(100, breadth), LayoutHarness.Measure(root, Space).Size);
        Approximately.Equal(new Offset(0, breadth / 2), line.Position);
        Approximately.Equal(new Offset(100, breadth / 2), line.End);
        Assert.Equal(style, line.Style);
        Assert.Equal(2f, line.Thickness);
        Assert.Equal(TestInks.Red, line.Ink);
    }

    [Fact]
    public void ADashedRuleTakesThePatternGiven()
    {
        Block root = LayoutHarness.Build(frame => frame.Rule(2, TestInks.Red, [4, 1, 1, 1]));

        LineOperation line = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>());

        Approximately.Equal(new Extent(100, 2), LayoutHarness.Measure(root, Space).Size);
        Approximately.Equal(new Offset(0, 1), line.Position);
        Assert.Equal([4f, 1f, 1f, 1f], line.Dashes!);
    }

    [Fact]
    public void ThePatternIsCopiedWhenTheRuleIsSet()
    {
        float[] dashes = [4, 1];
        Block root = LayoutHarness.Build(frame => frame.Rule(2, TestInks.Red, dashes));
        dashes[0] = 9;

        Assert.Equal([4f, 1f], Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>()).Dashes!);
    }

    [Fact]
    public void AWavyRuleThatDoesNotFitMovesOn()
    {
        Block root = LayoutHarness.Build(frame => frame.Rule(2, TestInks.Red, StrokeStyle.Wavy));

        Assert.True(LayoutHarness.Measure(root, new Extent(100, 5)).IsDeferred);
    }

    [Fact]
    public void AVerticalSolidRuleIsABar()
    {
        RectangleOperation bar = Assert.Single(LayoutHarness.Draw(frame => frame.VerticalRule(2, TestInks.Red), Space).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Extent(2, 30), bar.Size);
    }

    [Theory]
    [InlineData(StrokeStyle.Dashed, 2f)]
    [InlineData(StrokeStyle.Wavy, 6f)]
    public void AStyledVerticalRuleIsStrokedDownItsCentre(StrokeStyle style, float breadth)
    {
        Block root = LayoutHarness.Build(frame => frame.VerticalRule(2, TestInks.Red, style));

        LineOperation line = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>());

        Approximately.Equal(new Extent(breadth, 30), LayoutHarness.Measure(root, Space).Size);
        Approximately.Equal(new Offset(breadth / 2, 0), line.Position);
        Approximately.Equal(new Offset(breadth / 2, 30), line.End);
        Assert.Equal(style, line.Style);
    }

    [Fact]
    public void ADashedVerticalRuleTakesThePatternGiven()
    {
        Block root = LayoutHarness.Build(frame => frame.VerticalRule(2, TestInks.Red, [3, 3]));

        LineOperation line = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>());

        Approximately.Equal(new Offset(1, 0), line.Position);
        Assert.Equal([3f, 3f], line.Dashes!);
    }

    [Fact]
    public void AWavyVerticalRuleThatDoesNotFitMovesOn()
    {
        Block root = LayoutHarness.Build(frame => frame.VerticalRule(2, TestInks.Red, StrokeStyle.Wavy));

        Assert.True(LayoutHarness.Measure(root, new Extent(5, 100)).IsDeferred);
    }

    [Fact]
    public void ADashedRuleIsAsBroadAsItsWeightWhateverTheStyle()
    {
        // The pattern replaces the style, so a wave's extra breadth does not apply.
        RuleStroke stroke = new RuleStroke(2, TestInks.Red, StrokeStyle.Wavy, [1, 1]);

        Assert.Equal(2f, stroke.Breadth);
    }

    public static TheoryData<float[]> Unusable => new TheoryData<float[]>
    {
        new float[0],
        new float[] { 0, 0 },
        new float[] { 4, -1 },
        new float[] { float.NaN, 1 },
        new float[] { float.PositiveInfinity, 1 },
    };

    [Theory]
    [MemberData(nameof(Unusable))]
    public void APatternThatDrawsNoDashIsRefused(float[] dashes)
    {
        Assert.Throws<ArgumentException>(() => LayoutHarness.Build(frame => frame.Rule(1, TestInks.Red, dashes)));
        Assert.Throws<ArgumentException>(() => LayoutHarness.Build(frame => frame.VerticalRule(1, TestInks.Red, dashes)));
    }

    [Fact]
    public void APatternMustBeGiven() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Rule(1, TestInks.Red, (IReadOnlyList<float>)null!)));

    [Fact]
    public void AGapOfNothingIsAllowed()
    {
        Block root = LayoutHarness.Build(frame => frame.Rule(1, TestInks.Red, [2, 0]));

        Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<LineOperation>());
    }
}
