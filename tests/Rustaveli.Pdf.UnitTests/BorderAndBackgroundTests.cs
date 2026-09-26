namespace Rustaveli.Pdf.UnitTests;

public class BorderAndBackgroundTests
{
    [Fact]
    public void BackgroundCoversExactlyTheChildArea()
    {
        BackgroundElement element = new BackgroundElement { Color = Colors.Red, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation background = page.Operations.OfType<RectangleOperation>().First();

        Approximately.Equal(new Size(50, 20), background.Size);
        Assert.Equal(Colors.Red, background.Color);
    }

    [Fact]
    public void BackgroundDoesNotConsumeLayoutSpace()
    {
        BackgroundElement element = new BackgroundElement { Color = Colors.Red, Child = new FixedElement(50, 20) };

        Approximately.Equal(new Size(50, 20), LayoutHarness.Measure(element, new Size(200, 200)).Size);
    }

    [Fact]
    public void BorderDrawsOneBandPerRequestedSide()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        IEnumerable<RectangleOperation> borders = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Color == Colors.Black);

        Assert.Equal(4, borders.Count());
    }

    [Fact]
    public void BorderIsInsetWithinTheChildBounds()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.Zero.WithRight(3),
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation border = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Color == Colors.Black);

        Approximately.Equal(47f, border.Position.X);
        Approximately.Equal(3f, border.Size.Width);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void BackgroundPaintsNothingBehindAChildWithNothingToShow(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        BackgroundElement element = new BackgroundElement { Color = Colors.Red, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void BackgroundWithoutContentPaintsOnlyItsOwnFill()
    {
        BackgroundElement element = new BackgroundElement { Color = Colors.Red };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation fill = Assert.IsType<RectangleOperation>(Assert.Single(page.Operations));

        Assert.Equal(Colors.Red, fill.Color);
        Approximately.Equal(Position.Zero, fill.Position);
    }

    [Fact]
    public void BorderPlacesEachSideAlongItsOwnEdge()
    {
        BorderElement element = new BorderElement
        {
            Width = new Edges(1, 2, 3, 4),
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        // Offered exactly the content's size, so the box the sides trace is the same however it is decided.
        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        List<RectangleOperation> sides = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Color == Colors.Black).ToList();

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
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Equal(Colors.White, page.Operations.OfType<RectangleOperation>().First().Color);
    }

    [Fact]
    public void TransparentBorderDrawsOnlyTheContent()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            Color = Colors.Transparent,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation only = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(Colors.White, only.Color);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void BorderDrawsNothingAroundAChildWithNothingToShow(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        BorderElement element = new BorderElement { Width = Edges.All(2), Color = Colors.Black, Child = child };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations);
        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void BorderWithoutContentDrawsOnlyItsOwnSides()
    {
        BorderElement element = new BorderElement { Width = Edges.All(2), Color = Colors.Black };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Equal(4, page.Operations.Count);
        Assert.All(page.Operations, operation => Assert.Equal(Colors.Black, Assert.IsType<RectangleOperation>(operation).Color));
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
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));
        RoundedRectangleOperation outline = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(new Position(1, 1), outline.Position);
        Approximately.Equal(new Size(48, 18), outline.Size);
        Approximately.Equal(3f, outline.CornerRadius);
        Approximately.Equal(2f, outline.StrokeWidth);
        Assert.Equal(Colors.Black, outline.Color);
    }

    [Fact]
    public void RoundedBorderRadiusIsCappedAtHalfTheShorterSide()
    {
        BorderElement element = new BorderElement
        {
            Width = Edges.All(2),
            CornerRadius = 50,
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 20));

        // The 18pt-tall outline cannot turn a corner tighter than a semicircle.
        Approximately.Equal(9f, Assert.Single(page.Operations.OfType<RoundedRectangleOperation>()).CornerRadius);
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
            Color = Colors.Black,
            Child = new FixedElement(width, height, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(width, height));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Equal(Colors.White, Assert.Single(page.Operations.OfType<RectangleOperation>()).Color);
    }
}
