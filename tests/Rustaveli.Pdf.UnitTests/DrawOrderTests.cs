using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Content drawn in its draw order rather than the order it comes in, on pages that ask for it.
/// </summary>
public class DrawOrderTests
{
    private static readonly Ink Blue = Ink.Rgb(0, 0, 255);
    private static readonly Ink Green = Ink.Rgb(0, 255, 0);

    private static RecordedPage Render(Action<StackComposer> compose) =>
        Assert.Single(LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Margins = Sides.All(10);
            section.Body().Stack(compose);
        }))).Pages);

    private static List<Ink> Inks(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Select(operation => operation.Ink).ToList();

    [Fact]
    public void AHigherOrderIsDrawnAfterWhatFollowsIt()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).DrawOrder(1).Fill(TestInks.Red).Blank();
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        Assert.Equal([Ink.White, Blue, TestInks.Red], Inks(page));
    }

    [Fact]
    public void ALowerOrderIsDrawnBeneathTheRestButAboveThePaper()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).Fill(Blue).Blank();
            stack.Add().Height(20).DrawOrder(-1).Fill(TestInks.Red).Blank();
        });

        Assert.Equal([Ink.White, TestInks.Red, Blue], Inks(page));
    }

    [Fact]
    public void ContentOfOneOrderKeepsTheOrderItCameIn()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).DrawOrder(1).Fill(TestInks.Red).Blank();
            stack.Add().Height(20).DrawOrder(1).Fill(Green).Blank();
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        Assert.Equal([Ink.White, Blue, TestInks.Red, Green], Inks(page));
    }

    [Fact]
    public void HeldContentIsDrawnWhereItWasPlaced()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).Fill(Blue).Blank();
            stack.Add().Height(30).InsetLeft(15).DrawOrder(2).Fill(TestInks.Red).Blank();
            stack.Add().Height(20).Fill(Green).Blank();
        });

        RectangleOperation red = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red);
        Assert.Equal(new Bounds(25, 30, 190, 60), red.Bounds);
    }

    [Fact]
    public void AnInnerOrderOverridesAnOuterOneOnlyWithinIt()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().DrawOrder(2).Stack(inner =>
            {
                inner.Add().Height(20).Fill(TestInks.Red).Blank();
                inner.Add().Height(20).DrawOrder(-1).Fill(Green).Blank();
                inner.Add().Height(20).Fill(Ink.Rgb(9, 9, 9)).Blank();
            });
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        Assert.Equal([Ink.White, Green, Blue, TestInks.Red, Ink.Rgb(9, 9, 9)], Inks(page));
    }

    [Fact]
    public void AGradientIsDrawnWithTheContentItPaints()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).DrawOrder(1).Fill(Gradient.Across(TestInks.Red, Blue)).Blank();
            stack.Add().Height(20).Fill(Green).Blank();
        });

        List<DrawOperation> operations = page.Operations;
        int begin = operations.FindIndex(operation => operation is GradientOperation);

        Assert.True(begin > operations.FindIndex(operation => operation is RectangleOperation { Ink: var ink } && ink == Green));
        Assert.IsType<RectangleOperation>(operations[begin + 1]);
        Assert.IsType<GradientEndOperation>(operations[begin + 2]);
    }

    [Fact]
    public void TextImagesLinksAndShapesAreAllHeldInOrder()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().DrawOrder(1).Stack(inner =>
            {
                inner.Add().Text("Held");
                inner.Add().Height(10).Link("https://example.com").Blank();
                inner.Add().Height(10).Anchor("here").Blank();
                inner.Add().Height(10).CrossReference("here").Blank();
                inner.Add().Width(20).Image(new FakeImage(4, 2));
                inner.Add().Height(10).Rule(1, TestInks.Red, StrokeStyle.Dashed);
                inner.Add().Height(10).Rule(1, TestInks.Red, [2, 1]);
                inner.Add().Height(10).DropShadow(TestInks.Red, 2).Stroke(1).RoundCorners(2).Blank();
            });
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        List<DrawOperation> operations = page.Operations;
        int blue = operations.FindIndex(operation => operation is RectangleOperation { Ink: var ink } && ink == Blue);

        Assert.All(operations.Skip(blue + 1), operation => Assert.False(operation is RectangleOperation { Ink: var ink } && ink == Blue));
        Assert.Contains(operations.Skip(blue + 1), operation => operation is TextOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is ExternalLinkOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is DestinationOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is InternalLinkOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is ImageOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is LineOperation { Dashes: null });
        Assert.Contains(operations.Skip(blue + 1), operation => operation is LineOperation { Dashes: not null });
        Assert.Contains(operations.Skip(blue + 1), operation => operation is ShadowOperation);
        Assert.Contains(operations.Skip(blue + 1), operation => operation is RoundedRectangleOperation);
    }

    [Fact]
    public void TransformsAndClipsAreKeptForHeldContent()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(40).DrawOrder(1).Scale(0.5f).Compose(inner => inner.Slot().Child = new FixedBlock(40, 20, TestInks.Red));
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        // Half size, where the first item of the stack begins.
        Assert.Equal(new Bounds(10, 10, 30, 20), page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red).Bounds);
    }

    [Fact]
    public void HeldDrawingIsReplayedUnderItsOwnStateAndLeavesNoneBehind()
    {
        using RecordingSurface pages = new RecordingSurface();
        LayeredPageSink layers = new LayeredPageSink(pages);

        layers.BeginPage(new Extent(100, 100));
        layers.MoveOrigin(new Offset(10, 20));
        layers.Save();
        layers.RotateClockwise(90);
        layers.ClipRectangle(new Extent(5, 5));
        layers.Order = 1;
        layers.FillRectangle(Offset.Zero, new Extent(5, 5), TestInks.Red);
        layers.FillRectangle(new Offset(5, 0), new Extent(5, 5), Green);
        layers.Restore();
        layers.Order = 0;
        layers.FillRectangle(Offset.Zero, new Extent(5, 5), Blue);
        layers.EndPage();

        List<RectangleOperation> drawn = pages.Pages[0].Operations.OfType<RectangleOperation>().ToList();
        Assert.Equal([Blue, TestInks.Red, Green], drawn.Select(operation => operation.Ink));
        Approximately.Equal(new Offset(10, 20), drawn[0].Position);
        Approximately.Equal(new Offset(10, 20), drawn[1].Position);
        Approximately.Equal(new Offset(10, 25), drawn[2].Position);
        Assert.True(pages.IsAtIdentity);
        Assert.Equal(0, pages.PendingSaves);
    }

    [Fact]
    public void PagesWithoutADrawOrderAreDrawnAsTheyCome()
    {
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).Fill(TestInks.Red).Blank();
            stack.Add().Height(20).Fill(Blue).Blank();
        });

        Assert.Equal([Ink.White, TestInks.Red, Blue], Inks(page));
    }

    [Fact]
    public void ADrawOrderOutsideAPageDrawsItsContent()
    {
        DrawOrderBlock element = new DrawOrderBlock { Order = 3, Child = new FixedBlock(20, 20, TestInks.Red) };

        Assert.Single(LayoutHarness.Draw(element, new Extent(50, 50)).Operations);
    }

    [Fact]
    public void AnOrderSetByContentComposedLaterIsKept()
    {
        // The content is composed only when layout reaches it, so it cannot be found in the document beforehand.
        RecordedPage page = Render(stack =>
        {
            stack.Add().Height(20).Fill(Blue).Blank();
            stack.Add().ComposeLater(later => later.Height(20).DrawOrder(-1).Fill(TestInks.Red).Blank());
        });

        Assert.Equal([Ink.White, TestInks.Red, Blue], Inks(page));
    }

    [Fact]
    public void EveryPageStartsAfresh()
    {
        List<RecordedPage> pages = LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(100, 100);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(60).DrawOrder(1).Fill(TestInks.Red).Blank();
                stack.Add().Height(60).Fill(Blue).Blank();
            });
        }))).Pages;

        Assert.Equal(2, pages.Count);
        Assert.Equal([Ink.White, TestInks.Red], Inks(pages[0]));
        Assert.Equal([Ink.White, Blue], Inks(pages[1]));
    }

    [Fact]
    public void AnOrderIsRestoredWhenItsContentThrows()
    {
        using RecordingSurface pages = new RecordingSurface();
        LayeredPageSink layers = new LayeredPageSink(pages) { Order = 4 };
        DrawOrderBlock element = new DrawOrderBlock { Order = 9, Child = new ThrowingBlock(new InvalidOperationException("Broken.")) };

        Assert.Throws<InvalidOperationException>(() => element.Render(new Extent(10, 10), new RenderContext(layers, LayoutHarness.Context())));

        Assert.Equal(4, layers.Order);
    }
}
