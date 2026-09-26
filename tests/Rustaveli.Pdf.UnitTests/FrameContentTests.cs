namespace Rustaveli.Pdf.UnitTests;

public class FrameContentTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static void Fill(IFrame container, float width, float height, Ink color) =>
        container.Compose(inner => inner.Slot().Child = new FixedBlock(width, height, color));

    public static TheoryData<string, Action<IFrame>> CallsWithoutAHandler => new()
    {
        { nameof(FrameContent.Text), container => container.Text((Action<TextComposer>)null!) },
        { nameof(FrameContent.Stack), container => container.Stack(null!) },
        { nameof(FrameContent.Columns), container => container.Columns(null!) },
        { nameof(FrameContent.Table), container => container.Table(null!) },
        { nameof(FrameContent.List), container => container.List(null!) },
        { nameof(FrameContent.Layered), container => container.Layered(null!) },
        { nameof(FrameContent.Banded), container => container.Banded(null!) },
        { nameof(FrameContent.Compose), container => container.Compose(null!) },
    };

    [Theory]
    [MemberData(nameof(CallsWithoutAHandler))]
    public void RefusesAMissingHandlerBeforeAttachingAnything(string method, Action<IFrame> call)
    {
        Frame container = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => call(container));

        Assert.Equal("handler", exception.ParamName);

        // Rejecting after attaching would leave a half-built element occupying the slot.
        Assert.True(container.Slot().Child is null, $"{method} attached an element before rejecting its handler.");
    }

    [Fact]
    public void RefusesAMissingImage()
    {
        Frame container = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => container.Image(null!));

        Assert.Equal("image", exception.ParamName);
        Assert.Null(container.Slot().Child);
    }

    [Fact]
    public void AnImageFitsTheAvailableWidthByDefault()
    {
        Block root = LayoutHarness.Build(container => container.Image(new FakeImage(400, 200)));

        ImageOperation image = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<ImageOperation>());

        // 2:1 across the 200pt width.
        Approximately.Equal(new Extent(200, 100), image.Size);
    }

    [Fact]
    public void AnImageHonoursTheRequestedFit()
    {
        // A 1:2 image fitted to the width would need 400pt of height; fitted to the height it needs only 100pt.
        Block root = LayoutHarness.Build(container => container.Image(new FakeImage(100, 200), ImageFitting.FitHeight));

        ImageOperation image = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<ImageOperation>());

        Approximately.Equal(new Extent(100, 200), image.Size);
    }

    [Fact]
    public void RefusesAMissingSnippet()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new Frame().Snippet(null!));

        Assert.Equal("component", exception.ParamName);
    }

    [Fact]
    public void BandsFrameTheBodyBetweenHeadAndFoot()
    {
        Block root = LayoutHarness.Build(container => container.Banded(decoration =>
        {
            Fill(decoration.Head(), 50, 10, TestInks.Red);
            Fill(decoration.Body(), 50, 20, TestInks.Blue);
            Fill(decoration.Foot(), 50, 5, TestInks.Green);
        }));

        List<RectangleOperation> rectangles =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        float before = rectangles.Single(r => r.Ink == TestInks.Red).Position.Y;
        float content = rectangles.Single(r => r.Ink == TestInks.Blue).Position.Y;
        float after = rectangles.Single(r => r.Ink == TestInks.Green).Position.Y;

        // Only the stacking order is asserted: where the bands end up within the space they are given is the
        // decoration element's business, not the fluent API's.
        Approximately.Equal(0f, before);
        Assert.True(
            before < content && content < after,
            $"Expected before < content < after, got {before}, {content}, {after}.");
    }

    [Fact]
    public void ComposesASnippetIntoTheFrame()
    {
        Block root = LayoutHarness.Build(container => container.Inset(5).Snippet(new CaptionSnippet("Total")));

        TextOperation text = Assert.Single(LayoutHarness.Draw(root, Space).Texts);

        Assert.Equal("Total", text.Text);

        // Lands inside the padding exactly as inline composition would.
        Approximately.Equal(5f, text.Position.X);
    }

    [Fact]
    public void ConstructsAndComposesASnippetGivenOnlyItsType()
    {
        Block root = LayoutHarness.Build(container => container.Snippet<CaptionSnippet>());

        Assert.Equal(CaptionSnippet.DefaultCaption, LayoutHarness.Draw(root, Space).Content);
    }

    [Fact]
    public void RefusesASnippetComposedTwiceIntoOneSlot()
    {
        Frame container = new Frame();
        container.Snippet(new CaptionSnippet("first"));

        Assert.Throws<CompositionException>(() => container.Snippet(new CaptionSnippet("second")));
        Assert.Equal("first", LayoutHarness.Draw(container, Space).Content);
    }

    [Fact]
    public void ComposeHandsTheSameFrameToTheCompositionFunction()
    {
        Frame container = new Frame();
        IFrame? received = null;

        container.Compose(inner => received = inner);

        Assert.Same(container, received);
    }

    [Fact]
    public void RefusesToBlankAFilledFrame()
    {
        Frame container = new Frame();
        container.Text("already here");

        CompositionException exception = Assert.Throws<CompositionException>(() => container.Blank());

        Assert.Contains("This frame already holds TextBlock, so it cannot be left blank", exception.Message);
        Assert.IsType<TextBlock>(container.Slot().Child);
    }

    [Fact]
    public void RefusesToBlankAMissingFrame()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => FrameContent.Blank(null!));

        Assert.Equal("parent", exception.ParamName);
    }
}
