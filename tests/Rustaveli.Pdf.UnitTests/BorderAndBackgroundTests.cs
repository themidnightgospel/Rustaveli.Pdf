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
}
