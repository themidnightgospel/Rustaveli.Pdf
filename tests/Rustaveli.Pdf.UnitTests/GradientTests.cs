namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Gradients: what they accept, where their blend runs, and the frames, strokes and rules painted with them.
/// </summary>
public class GradientTests
{
    private static readonly Ink Blue = Ink.Rgb(0, 0, 255);

    // ---- The gradient ----------------------------------------------------------------------------------------

    [Fact]
    public void AGradientKeepsItsAngleAndInks()
    {
        Gradient gradient = new Gradient(30, TestInks.Red, Blue, TestInks.Black);

        Assert.Equal(30f, gradient.Angle);
        Assert.Equal([TestInks.Red, Blue, TestInks.Black], gradient.Inks);
        Assert.Equal(1f, gradient.Opacity);
    }

    [Fact]
    public void AcrossAndDownAreTheTwoAxes()
    {
        Assert.Equal(0f, Gradient.Across(TestInks.Red, Blue).Angle);
        Assert.Equal(90f, Gradient.Down(TestInks.Red, Blue).Angle);
    }

    [Fact]
    public void TheInksAreCopied()
    {
        Ink[] inks = [TestInks.Red, Blue];
        Gradient gradient = Gradient.Across(inks);
        inks[0] = TestInks.Black;

        Assert.Equal(TestInks.Red, gradient.Inks[0]);
    }

    [Fact]
    public void AGradientBlendsAtLeastTwoInks()
    {
        Assert.Throws<ArgumentException>(() => Gradient.Across(TestInks.Red));
        Assert.Throws<ArgumentException>(() => Gradient.Across());
        Assert.Throws<ArgumentNullException>(() => Gradient.Across(null!));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void AnAngleMustBeANumber(float angle) =>
        Assert.Throws<ArgumentException>(() => new Gradient(angle, TestInks.Red, Blue));

    [Fact]
    public void TheInksShareOneOpacity()
    {
        Assert.Throws<ArgumentException>(() => Gradient.Across(TestInks.Red, Blue.WithOpacity(0.5f)));
        Assert.Equal(0.5f, Gradient.Across(TestInks.Red.WithOpacity(0.5f), Blue.WithOpacity(0.5f)).Opacity);
    }

    [Theory]
    [InlineData(0f, 10f, 45f, 110f, 45f)]
    [InlineData(90f, 60f, 20f, 60f, 70f)]
    [InlineData(180f, 110f, 45f, 10f, 45f)]
    [InlineData(270f, 60f, 70f, 60f, 20f)]
    [InlineData(-90f, 60f, 70f, 60f, 20f)]
    public void TheBlendRunsThroughTheCentreAtItsAngle(float angle, float startX, float startY, float endX, float endY)
    {
        (Offset start, Offset end) = new Gradient(angle, TestInks.Red, Blue).Axis(new Offset(10, 20), new Extent(100, 50));

        Approximately.Equal(new Offset(startX, startY), start);
        Approximately.Equal(new Offset(endX, endY), end);
    }

    [Fact]
    public void AnAngledBlendReachesTheCorners()
    {
        // At 45 degrees across a square, the line runs corner to corner.
        (Offset start, Offset end) = new Gradient(45, TestInks.Red, Blue).Axis(Offset.Zero, new Extent(100, 100));

        Approximately.Equal(Offset.Zero, start);
        Approximately.Equal(new Offset(100, 100), end);
    }

    [Fact]
    public void EvenlySpacedInksLieFromStartToEnd() =>
        Assert.Equal([0f, 0.5f, 1f], new Gradient(0, TestInks.Red, Blue, TestInks.Black).Positions);

    [Fact]
    public void StopsLieWhereTheySay()
    {
        Gradient gradient = new Gradient(45, new GradientStop(0.2f, TestInks.Red), new GradientStop(0.9f, Blue));

        Assert.Equal([0.2f, 0.9f], gradient.Positions);
        Assert.Equal([TestInks.Red, Blue], gradient.Inks);
        Assert.Equal(45f, gradient.Angle);
    }

    public static TheoryData<GradientStop[]> UnusableStops => new TheoryData<GradientStop[]>
    {
        new[] { new GradientStop(0, TestInks.Red) },
        new[] { new GradientStop(-0.1f, TestInks.Red), new GradientStop(1, Blue) },
        new[] { new GradientStop(0, TestInks.Red), new GradientStop(1.5f, Blue) },
        new[] { new GradientStop(0.6f, TestInks.Red), new GradientStop(0.4f, Blue) },
        new[] { new GradientStop(float.NaN, TestInks.Red), new GradientStop(1, Blue) },
        new[] { new GradientStop(0, TestInks.Red), new GradientStop(1, Blue.WithOpacity(0.5f)) },
    };

    [Theory]
    [MemberData(nameof(UnusableStops))]
    public void StopsMustLieInOrderWithinTheBlend(GradientStop[] stops) =>
        Assert.Throws<ArgumentException>(() => new Gradient(0, stops));

    [Fact]
    public void StopsAreNeeded()
    {
        Assert.Throws<ArgumentNullException>(() => new Gradient(0, (GradientStop[])null!));
        Assert.Throws<ArgumentException>(() => new Gradient(float.NaN, new GradientStop(0, TestInks.Red), new GradientStop(1, Blue)));
    }

    [Fact]
    public void ABlendBetweenPointsOfTheBoxFollowsTheBox()
    {
        Gradient gradient = Gradient.Between(new Offset(0.5f, 0), new Offset(0.5f, 1), ofBox: true, [new GradientStop(0, TestInks.Red), new GradientStop(1, Blue)]);

        Assert.Equal((new Offset(60, 20), new Offset(60, 70)), gradient.Axis(new Offset(10, 20), new Extent(100, 50)));
    }

    [Fact]
    public void ABlendBetweenPointsInSpaceIgnoresTheBox()
    {
        Gradient gradient = Gradient.Between(new Offset(1, 2), new Offset(3, 4), ofBox: false, [new GradientStop(0, TestInks.Red), new GradientStop(1, Blue.WithOpacity(0.5f))]);

        Assert.Equal((new Offset(1, 2), new Offset(3, 4)), gradient.Axis(new Offset(10, 20), new Extent(100, 50)));
        Assert.Equal(0.75f, gradient.Opacity);
        Assert.Throws<ArgumentException>(() => Gradient.Between(Offset.Zero, Offset.Zero, true, [new GradientStop(0, TestInks.Red)]));
    }

    // ---- Painting with it --------------------------------------------------------------------------------------

    private static readonly Gradient Blend = Gradient.Across(TestInks.Red, Blue);

    [Fact]
    public void AFillPaintsItsWholeBoxInTheGradient()
    {
        FillBlock element = new FillBlock { Gradient = Blend, Child = new FixedBlock(50, 20, TestInks.White) };

        List<DrawOperation> operations = LayoutHarness.Draw(element, new Extent(50, 20)).Operations;

        GradientOperation gradient = Assert.IsType<GradientOperation>(operations[0]);
        Assert.Same(Blend, gradient.Gradient);
        Assert.Equal(new Bounds(0, 0, 50, 20), gradient.Bounds);
        Assert.Equal(new Extent(50, 20), Assert.IsType<RectangleOperation>(operations[1]).Size);
        Assert.IsType<GradientEndOperation>(operations[2]);
        Assert.Equal(TestInks.White, Assert.IsType<RectangleOperation>(operations[3]).Ink);
    }

    [Fact]
    public void ARoundedFillKeepsItsCornersInTheGradient()
    {
        FillBlock element = new FillBlock { Gradient = Blend, Corners = Corners.All(4), Child = new FixedBlock(50, 20) };

        List<DrawOperation> operations = LayoutHarness.Draw(element, new Extent(50, 20)).Operations;

        Assert.Equal(Corners.All(4), Assert.IsType<RoundedRectangleOperation>(operations[1]).Corners);
    }

    [Fact]
    public void AGradientIsPaintedWhateverTheInk()
    {
        FillBlock element = new FillBlock { Gradient = Blend, Ink = Ink.Transparent, Child = new FixedBlock(50, 20) };

        Assert.Single(LayoutHarness.Draw(element, new Extent(50, 20)).Operations.OfType<GradientOperation>());
    }

    [Fact]
    public void AStrokeLaysOneBlendAcrossEverythingItCovers()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            Gradient = Blend,
            Alignment = StrokeAlignment.Outside,
            Child = new FixedBlock(50, 20),
        };

        List<DrawOperation> operations = LayoutHarness.Draw(element, new Extent(50, 20)).Operations;
        GradientOperation gradient = Assert.Single(operations.OfType<GradientOperation>());
        int begin = operations.IndexOf(gradient);

        Assert.Equal(new Bounds(-2, -2, 52, 22), gradient.Bounds);
        Assert.All(operations.Skip(begin + 1).Take(4), side => Assert.IsType<RectangleOperation>(side));
        Assert.IsType<GradientEndOperation>(operations[begin + 5]);
    }

    [Fact]
    public void ARoundedStrokeIsPaintedInTheGradient()
    {
        StrokeBlock element = new StrokeBlock { Weight = Sides.All(2), Gradient = Blend, Corners = Corners.All(5), Child = new FixedBlock(50, 20) };

        List<DrawOperation> operations = LayoutHarness.Draw(element, new Extent(50, 20)).Operations;

        Assert.Equal(new Bounds(0, 0, 50, 20), Assert.Single(operations.OfType<GradientOperation>()).Bounds);
        Assert.Single(operations.OfType<RoundedRectangleOperation>());
    }

    [Fact]
    public void AStrokeWithNeitherInkNorGradientIsNotDrawn()
    {
        StrokeBlock element = new StrokeBlock { Weight = Sides.All(2), Ink = Ink.Transparent };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(50, 20)).Operations);
    }

    [Fact]
    public void ARuleIsPaintedAlongItsLength()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.Rule(2, Blend), new Extent(100, 30)).Operations;

        Assert.Equal(new Bounds(0, 0, 100, 2), Assert.IsType<GradientOperation>(operations[0]).Bounds);
        Assert.IsType<RectangleOperation>(operations[1]);
        Assert.IsType<GradientEndOperation>(operations[2]);
    }

    [Fact]
    public void AWavyRuleIsPaintedAcrossItsWholeBreadth()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.Rule(2, Blend, StrokeStyle.Wavy), new Extent(100, 30)).Operations;

        Assert.Equal(new Bounds(0, 0, 100, 6), Assert.IsType<GradientOperation>(operations[0]).Bounds);
        Assert.Equal(StrokeStyle.Wavy, Assert.IsType<LineOperation>(operations[1]).Style);
    }

    [Fact]
    public void ADashedRuleIsPaintedInTheGradient()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.Rule(2, Blend, [3, 1]), new Extent(100, 30)).Operations;

        Assert.Equal([3f, 1f], Assert.IsType<LineOperation>(operations[1]).Dashes!);
    }

    [Fact]
    public void AVerticalRuleIsPaintedDownItsLength()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.VerticalRule(2, Blend), new Extent(100, 30)).Operations;

        Assert.Equal(new Bounds(0, 0, 2, 30), Assert.IsType<GradientOperation>(operations[0]).Bounds);
        Assert.Equal(new Extent(2, 30), Assert.IsType<RectangleOperation>(operations[1]).Size);
    }

    [Fact]
    public void ADashedVerticalRuleIsPaintedInTheGradient()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.VerticalRule(2, Blend, [3, 1]), new Extent(100, 30)).Operations;

        Assert.IsType<GradientOperation>(operations[0]);
        Assert.Equal([3f, 1f], Assert.IsType<LineOperation>(operations[1]).Dashes!);
    }

    [Fact]
    public void AStyledVerticalRuleIsPaintedInTheGradient()
    {
        List<DrawOperation> operations = LayoutHarness.Draw(frame => frame.VerticalRule(2, Blend, StrokeStyle.Dotted), new Extent(100, 30)).Operations;

        Assert.Equal(StrokeStyle.Dotted, Assert.IsType<LineOperation>(operations[1]).Style);
    }

    // ---- The frame's methods -----------------------------------------------------------------------------------

    [Fact]
    public void FillTakesAGradient()
    {
        Block root = LayoutHarness.Build(frame => frame.Fill(Blend).Compose(inner => { }));

        Assert.Same(Blend, Assert.IsType<FillBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child).Gradient);
    }

    [Fact]
    public void StrokeInkTakesAGradient()
    {
        Block root = LayoutHarness.Build(frame => frame.Stroke(1).StrokeInk(Blend).Compose(inner => { }));

        Assert.Same(Blend, Assert.IsType<StrokeBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child).Gradient);
    }

    [Fact]
    public void AGradientStrokeInkMustFollowAStroke()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => LayoutHarness.Build(frame => frame.Inset(1).StrokeInk(Blend)));

        Assert.Contains("StrokeInk must directly follow Stroke", exception.Message);
    }

    [Fact]
    public void EveryGradientMethodNeedsAGradient()
    {
        Gradient none = null!;

        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Fill(none)));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Stroke(1).StrokeInk(none)));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Rule(1, none)));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Rule(1, none, [1, 1])));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.VerticalRule(1, none)));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.VerticalRule(1, none, [1, 1])));
    }
}
