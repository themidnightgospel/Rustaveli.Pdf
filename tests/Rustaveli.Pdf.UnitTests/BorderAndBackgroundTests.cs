namespace Rustaveli.Pdf.UnitTests;

public class BorderAndBackgroundTests
{
    [Fact]
    public void BackgroundFillsTheBoxItIsGivenRatherThanItsContent()
    {
        BackgroundElement element = new BackgroundElement { Color = TestInks.Red, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation background = page.Operations.OfType<RectangleOperation>().First();

        // The box a parent allots is the box this element occupies (ADR 0012).
        Approximately.Equal(new Size(200, 200), background.Size);
        Assert.Equal(TestInks.Red, background.Color);
    }

    [Fact]
    public void BackgroundDoesNotConsumeLayoutSpace()
    {
        BackgroundElement element = new BackgroundElement { Color = TestInks.Red, Child = new FixedElement(50, 20) };

        Approximately.Equal(new Size(50, 20), LayoutHarness.Measure(element, new Size(200, 200)).Size);
    }

    [Fact]
    public void BorderDrawsOneBandPerRequestedSide()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        IEnumerable<RectangleOperation> borders = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Color == TestInks.Black);

        Assert.Equal(4, borders.Count());
    }

    [Fact]
    public void BorderIsInsetWithinTheBoxItIsGiven()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.Zero.WithRight(3),
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        RectangleOperation border = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Color == TestInks.Black);

        Approximately.Equal(47f, border.Position.X);
        Approximately.Equal(3f, border.Size.Width);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void BackgroundPaintsNothingBehindAChildWithNothingToShow(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        BackgroundElement element = new BackgroundElement { Color = TestInks.Red, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void BackgroundWithoutContentPaintsOnlyItsOwnFill()
    {
        BackgroundElement element = new BackgroundElement { Color = TestInks.Red };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation fill = Assert.IsType<RectangleOperation>(Assert.Single(page.Operations));

        Assert.Equal(TestInks.Red, fill.Color);
        Approximately.Equal(Position.Zero, fill.Position);
    }

    [Fact]
    public void BorderPlacesEachSideAlongItsOwnEdge()
    {
        BorderElement element = new BorderElement
        {
            Width = new Edges(1, 2, 3, 4),
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        // Offered exactly the content's size, so the box the sides trace is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        List<RectangleOperation> sides = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Color == TestInks.Black).ToList();

        Assert.Equal(4, sides.Count);
        Assert.Equal(new Bounds(0, 0, 1, 20), sides[0].Bounds);
        Assert.Equal(new Bounds(0, 0, 50, 2), sides[1].Bounds);
        Assert.Equal(new Bounds(47, 0, 50, 20), sides[2].Bounds);
        Assert.Equal(new Bounds(0, 16, 50, 20), sides[3].Bounds);
    }

    [Fact]
    public void BorderIsDrawnOverTheContent()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Equal(TestInks.White, page.Operations.OfType<RectangleOperation>().First().Color);
    }

    [Fact]
    public void TransparentBorderDrawsOnlyTheContent()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            Color = TestInks.Transparent,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation only = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.White, only.Color);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void BorderDrawsNothingAroundAChildWithNothingToShow(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        BorderElement element = new BorderElement { Width = Edges.All(2), Color = TestInks.Black, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void BorderWithoutContentDrawsOnlyItsOwnSides()
    {
        BorderElement element = new BorderElement { Width = Edges.All(2), Color = TestInks.Black };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Equal(4, page.Operations.Count);
        Assert.All(page.Operations, operation => Assert.Equal(TestInks.Black, Assert.IsType<RectangleOperation>(operation).Color));
    }

    [Fact]
    public void RoundedBorderIsStrokedAlongItsCentreline()
    {
        // Inset by half the 2pt stroke, with the radius reduced to match, so the outer edge of the stroke lands
        // on the requested 4pt radius.
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            CornerRadius = 4,
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        RoundedRectangleOperation outline = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(new Position(1, 1), outline.Position);
        Approximately.Equal(new Size(48, 18), outline.Size);
        Approximately.Equal(3f, outline.CornerRadius);
        Approximately.Equal(2f, outline.StrokeWidth);
        Assert.Equal(TestInks.Black, outline.Color);
    }

    [Fact]
    public void RoundedBorderRadiusIsCappedAtHalfTheShorterSide()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            CornerRadius = 50,
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));

        // The 18pt-tall outline cannot turn a corner tighter than a semicircle.
        Approximately.Equal(9f, Assert.Single(page.Operations.OfType<RoundedRectangleOperation>()).CornerRadius);
    }

    [Theory]
    [InlineData(4f, 1f, 1f, 1f)]
    [InlineData(1f, 1f, 4f, 1f)]
    [InlineData(1f, 1f, 1f, 4f)]
    public void RoundedCornersNeedEverySideTheSameWidth(float left, float top, float right, float bottom)
    {
        BorderElement element = new BorderElement
        {
            Width = new Edges(left, top, right, bottom),
            CornerRadius = 5,
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Equal(4, page.Operations.OfType<RectangleOperation>().Count(operation => operation.Color == TestInks.Black));
    }

    [Fact]
    public void ABorderWithNoWidthDrawsNothingEvenWhenRounded()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.Zero,
            CornerRadius = 5,
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));

        Assert.Equal(TestInks.White, Assert.IsType<RectangleOperation>(Assert.Single(page.Operations)).Color);
    }

    [Theory]
    [InlineData(40f, 20f)]
    [InlineData(20f, 40f)]
    [InlineData(30f, 40f)]
    public void RoundedBorderAtLeastAsThickAsItsBoxIsNotDrawn(float width, float height)
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(30),
            CornerRadius = 5,
            Color = TestInks.Black,
            Child = new FixedElement(width, height, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(width, height));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Equal(TestInks.White, Assert.Single(page.Operations.OfType<RectangleOperation>()).Color);
    }
}
