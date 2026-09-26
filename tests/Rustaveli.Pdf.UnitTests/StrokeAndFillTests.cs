namespace Rustaveli.Pdf.UnitTests;

public class StrokeAndFillTests
{
    [Fact]
    public void FillPaintsTheBoxItIsGivenRatherThanItsContent()
    {
        FillBlock element = new FillBlock { Ink = TestInks.Red, Child = new FixedBlock(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        RectangleOperation background = page.Operations.OfType<RectangleOperation>().First();

        // The box a parent allots is the box this element occupies (ADR 0012).
        Approximately.Equal(new Extent(200, 200), background.Size);
        Assert.Equal(TestInks.Red, background.Ink);
    }

    [Fact]
    public void FillDoesNotConsumeLayoutSpace()
    {
        FillBlock element = new FillBlock { Ink = TestInks.Red, Child = new FixedBlock(50, 20) };

        Approximately.Equal(new Extent(50, 20), LayoutHarness.Measure(element, new Extent(200, 200)).Size);
    }

    [Fact]
    public void StrokeDrawsOneBandPerRequestedSide()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        IEnumerable<RectangleOperation> borders = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Ink == TestInks.Black);

        Assert.Equal(4, borders.Count());
    }

    [Fact]
    public void StrokeIsInsetWithinTheBoxItIsGiven()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.Zero.WithRight(3),
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RectangleOperation border = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Black);

        Approximately.Equal(47f, border.Position.X);
        Approximately.Equal(3f, border.Size.Width);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void FillPaintsNothingBehindAChildWithNothingToShow(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        FillBlock element = new FillBlock { Ink = TestInks.Red, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void FillWithoutContentPaintsOnlyItsOwnFill()
    {
        FillBlock element = new FillBlock { Ink = TestInks.Red };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        RectangleOperation fill = Assert.IsType<RectangleOperation>(Assert.Single(page.Operations));

        Assert.Equal(TestInks.Red, fill.Ink);
        Approximately.Equal(Offset.Zero, fill.Position);
    }

    [Fact]
    public void StrokePlacesEachSideAlongItsOwnEdge()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = new Sides(1, 2, 3, 4),
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        // Offered exactly the content's size, so the box the sides trace is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        List<RectangleOperation> sides = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Ink == TestInks.Black).ToList();

        Assert.Equal(4, sides.Count);
        Assert.Equal(new Bounds(0, 0, 1, 20), sides[0].Bounds);
        Assert.Equal(new Bounds(0, 0, 50, 2), sides[1].Bounds);
        Assert.Equal(new Bounds(47, 0, 50, 20), sides[2].Bounds);
        Assert.Equal(new Bounds(0, 16, 50, 20), sides[3].Bounds);
    }

    [Fact]
    public void StrokeIsDrawnOverTheContent()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Equal(TestInks.White, page.Operations.OfType<RectangleOperation>().First().Ink);
    }

    [Fact]
    public void TransparentStrokeDrawsOnlyTheContent()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            Ink = TestInks.Transparent,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        RectangleOperation only = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.White, only.Ink);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void StrokeDrawsNothingAroundAChildWithNothingToShow(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        StrokeBlock element = new StrokeBlock { Weight = Sides.All(2), Ink = TestInks.Black, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void StrokeWithoutContentDrawsOnlyItsOwnSides()
    {
        StrokeBlock element = new StrokeBlock { Weight = Sides.All(2), Ink = TestInks.Black };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Equal(4, page.Operations.Count);
        Assert.All(page.Operations, operation => Assert.Equal(TestInks.Black, Assert.IsType<RectangleOperation>(operation).Ink));
    }

    [Fact]
    public void RoundedStrokeIsStrokedAlongItsCentreline()
    {
        // Inset by half the 2pt stroke, with the radius reduced to match, so the outer edge of the stroke lands
        // on the requested 4pt radius.
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            CornerRadius = 4,
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RoundedRectangleOperation outline = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(new Offset(1, 1), outline.Position);
        Approximately.Equal(new Extent(48, 18), outline.Size);
        Approximately.Equal(3f, outline.Radius);
        Approximately.Equal(2f, outline.StrokeWidth);
        Assert.Equal(TestInks.Black, outline.Ink);
    }

    [Fact]
    public void RoundedStrokeRadiusIsCappedAtHalfTheShorterSide()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(2),
            CornerRadius = 50,
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));

        // The 18pt-tall outline cannot turn a corner tighter than a semicircle.
        Approximately.Equal(9f, Assert.Single(page.Operations.OfType<RoundedRectangleOperation>()).Radius);
    }

    [Theory]
    [InlineData(4f, 1f, 1f, 1f)]
    [InlineData(1f, 1f, 4f, 1f)]
    [InlineData(1f, 1f, 1f, 4f)]
    public void RoundedCornersNeedEverySideTheSameWidth(float left, float top, float right, float bottom)
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = new Sides(left, top, right, bottom),
            CornerRadius = 5,
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Equal(4, page.Operations.OfType<RectangleOperation>().Count(operation => operation.Ink == TestInks.Black));
    }

    [Fact]
    public void AStrokeWithNoWidthDrawsNothingEvenWhenRounded()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.Zero,
            CornerRadius = 5,
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));

        Assert.Equal(TestInks.White, Assert.IsType<RectangleOperation>(Assert.Single(page.Operations)).Ink);
    }

    [Theory]
    [InlineData(40f, 20f)]
    [InlineData(20f, 40f)]
    [InlineData(30f, 40f)]
    public void RoundedStrokeAtLeastAsThickAsItsBoxIsNotDrawn(float width, float height)
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Sides.All(30),
            CornerRadius = 5,
            Ink = TestInks.Black,
            Child = new FixedBlock(width, height, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(width, height));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Equal(TestInks.White, Assert.Single(page.Operations.OfType<RectangleOperation>()).Ink);
    }
}
