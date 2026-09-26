using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class ContentExtensionsTests
{
    private static readonly Size Space = new Size(200, 200);

    private static void Fill(IContainer container, float width, float height, Ink color) =>
        container.Element(inner => inner.Child = new FixedElement(width, height, color));

    public static TheoryData<string, Action<IContainer>> CallsWithoutAHandler => new()
    {
        { nameof(ContentExtensions.Text), container => container.Text((Action<TextDescriptor>)null!) },
        { nameof(ContentExtensions.Column), container => container.Column(null!) },
        { nameof(ContentExtensions.Row), container => container.Row(null!) },
        { nameof(ContentExtensions.Table), container => container.Table(null!) },
        { nameof(ContentExtensions.List), container => container.List(null!) },
        { nameof(ContentExtensions.Layers), container => container.Layers(null!) },
        { nameof(ContentExtensions.Decoration), container => container.Decoration(null!) },
        { nameof(ContentExtensions.Element), container => container.Element(null!) },
    };

    [Theory]
    [MemberData(nameof(CallsWithoutAHandler))]
    public void RefusesAMissingHandlerBeforeAttachingAnything(string method, Action<IContainer> call)
    {
        Container container = new Container();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => call(container));

        Assert.Equal("handler", exception.ParamName);

        // Rejecting after attaching would leave a half-built element occupying the slot.
        Assert.True(container.Child is null, $"{method} attached an element before rejecting its handler.");
    }

    [Fact]
    public void RefusesAMissingImage()
    {
        Container container = new Container();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => container.Image(null!));

        Assert.Equal("image", exception.ParamName);
        Assert.Null(container.Child);
    }

    [Fact]
    public void AnImageFitsTheAvailableWidthByDefault()
    {
        Element root = LayoutHarness.Build(container => container.Image(new FakeImage(400, 200)));

        ImageOperation image = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<ImageOperation>());

        // 2:1 across the 200pt width.
        Approximately.Equal(new Size(200, 100), image.Size);
    }

    [Fact]
    public void AnImageHonoursTheRequestedFit()
    {
        // A 1:2 image fitted to the width would need 400pt of height; fitted to the height it needs only 100pt.
        Element root = LayoutHarness.Build(container => container.Image(new FakeImage(100, 200), ImageFit.Height));

        ImageOperation image = Assert.Single(LayoutHarness.Draw(root, Space).Operations.OfType<ImageOperation>());

        Approximately.Equal(new Size(100, 200), image.Size);
    }

    [Fact]
    public void RefusesAMissingComponent()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new Container().Component(null!));

        Assert.Equal("component", exception.ParamName);
    }

    [Fact]
    public void DecorationFramesTheContentBetweenItsBands()
    {
        Element root = LayoutHarness.Build(container => container.Decoration(decoration =>
        {
            Fill(decoration.Before(), 50, 10, TestInks.Red);
            Fill(decoration.Content(), 50, 20, TestInks.Blue);
            Fill(decoration.After(), 50, 5, TestInks.Green);
        }));

        List<RectangleOperation> rectangles =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        float before = rectangles.Single(r => r.Color == TestInks.Red).Position.Y;
        float content = rectangles.Single(r => r.Color == TestInks.Blue).Position.Y;
        float after = rectangles.Single(r => r.Color == TestInks.Green).Position.Y;

        // Only the stacking order is asserted: where the bands end up within the space they are given is the
        // decoration element's business, not the fluent API's.
        Approximately.Equal(0f, before);
        Assert.True(
            before < content && content < after,
            $"Expected before < content < after, got {before}, {content}, {after}.");
    }

    [Fact]
    public void ComposesAComponentIntoTheContainer()
    {
        Element root = LayoutHarness.Build(container => container.Padding(5).Component(new CaptionComponent("Total")));

        TextOperation text = Assert.Single(LayoutHarness.Draw(root, Space).Texts);

        Assert.Equal("Total", text.Text);

        // Lands inside the padding exactly as inline composition would.
        Approximately.Equal(5f, text.Position.X);
    }

    [Fact]
    public void ConstructsAndComposesAComponentGivenOnlyItsType()
    {
        Element root = LayoutHarness.Build(container => container.Component<CaptionComponent>());

        Assert.Equal(CaptionComponent.DefaultCaption, LayoutHarness.Draw(root, Space).Content);
    }

    [Fact]
    public void RefusesAComponentComposedTwiceIntoOneSlot()
    {
        Container container = new Container();
        container.Component(new CaptionComponent("first"));

        Assert.Throws<DocumentComposeException>(() => container.Component(new CaptionComponent("second")));
        Assert.Equal("first", LayoutHarness.Draw(container, Space).Content);
    }

    [Fact]
    public void ElementHandsTheSameSlotToTheCompositionFunction()
    {
        Container container = new Container();
        IContainer? received = null;

        container.Element(inner => received = inner);

        Assert.Same(container, received);
    }

    [Fact]
    public void RefusesToMarkAFilledContainerEmpty()
    {
        Container container = new Container();
        container.Text("already here");

        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() => container.Empty());

        Assert.Contains("already holds TextElement, so it cannot be marked empty", exception.Message);
        Assert.IsType<TextElement>(container.Child);
    }

    [Fact]
    public void RefusesToMarkAMissingContainerEmpty()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ContentExtensions.Empty(null!));

        Assert.Equal("parent", exception.ParamName);
    }
}
