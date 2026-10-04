namespace Rustaveli.Pdf.UnitTests;

public class FrameContentTests
{
    private static readonly Extent Space = new Extent(200, 200);

    private static void Fill(IFrame frame, float width, float height, Ink color) =>
        frame.Compose(inner => inner.Slot().Child = new FixedBlock(width, height, color));

    public static TheoryData<string, Action<IFrame>> CallsWithoutAHandler => new()
    {
        { nameof(FrameContent.Text), frame => frame.Text((Action<TextComposer>)null!) },
        { nameof(FrameContent.Stack), frame => frame.Stack(null!) },
        { nameof(FrameContent.Columns), frame => frame.Columns(null!) },
        { nameof(FrameContent.Table), frame => frame.Table(null!) },
        { nameof(FrameContent.List), frame => frame.List(null!) },
        { nameof(FrameContent.Layered), frame => frame.Layered(null!) },
        { nameof(FrameContent.Banded), frame => frame.Banded(null!) },
        { nameof(FrameContent.Compose), frame => frame.Compose(null!) },
    };

    [Theory]
    [MemberData(nameof(CallsWithoutAHandler))]
    public void RefusesAMissingHandlerBeforeAttachingAnything(string method, Action<IFrame> call)
    {
        Frame frame = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => call(frame));

        Assert.Equal("handler", exception.ParamName);

        // Rejecting after attaching would leave a half-built block occupying the slot.
        Assert.True(frame.Slot().Child is null, $"{method} attached a block before rejecting its handler.");
    }

    [Fact]
    public void RefusesAMissingImage()
    {
        Frame frame = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => frame.Image((IImage)null!));

        Assert.Equal("image", exception.ParamName);
        Assert.Null(frame.Slot().Child);
    }

    [Fact]
    public void AnImageFitsTheAvailableWidthByDefault()
    {
        Block root = LayoutHarness.Build(frame => frame.Image(new FakeImage(400, 200)));

        ImageOperation image = Assert.Single(LayoutHarness.Render(root, Space).Operations.OfType<ImageOperation>());

        // 2:1 across the 200pt width.
        Approximately.Equal(new Extent(200, 100), image.Size);
    }

    [Fact]
    public void AnImageHonoursTheRequestedFit()
    {
        // A 1:2 image fitted to the width would need 400pt of height; fitted to the height it needs only 100pt.
        Block root = LayoutHarness.Build(frame => frame.Image(new FakeImage(100, 200), ImageFitting.FitHeight));

        ImageOperation image = Assert.Single(LayoutHarness.Render(root, Space).Operations.OfType<ImageOperation>());

        Approximately.Equal(new Extent(100, 200), image.Size);
    }

    [Fact]
    public void RefusesAMissingSnippet()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new Frame().Snippet(null!));

        Assert.Equal("snippet", exception.ParamName);
    }

    [Fact]
    public void BandsFrameTheBodyBetweenHeadAndFoot()
    {
        Block root = LayoutHarness.Build(frame => frame.Banded(bands =>
        {
            Fill(bands.Head(), 50, 10, TestInks.Red);
            Fill(bands.Body(), 50, 20, TestInks.Blue);
            Fill(bands.Foot(), 50, 5, TestInks.Green);
        }));

        List<RectangleOperation> rectangles =
            LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().ToList();

        float before = rectangles.Single(r => r.Ink == TestInks.Red).Position.Y;
        float content = rectangles.Single(r => r.Ink == TestInks.Blue).Position.Y;
        float after = rectangles.Single(r => r.Ink == TestInks.Green).Position.Y;

        // Only the stacking order is asserted: where the bands end up within the space they are given is the
        // bands block's business, not the fluent API's.
        Approximately.Equal(0f, before);
        Assert.True(
            before < content && content < after,
            $"Expected before < content < after, got {before}, {content}, {after}.");
    }

    [Fact]
    public void ComposesASnippetIntoTheFrame()
    {
        Block root = LayoutHarness.Build(frame => frame.Inset(5).Snippet(new CaptionSnippet("Total")));

        TextOperation text = Assert.Single(LayoutHarness.Render(root, Space).Texts);

        Assert.Equal("Total", text.Text);

        // Lands inside the padding exactly as inline composition would.
        Approximately.Equal(5f, text.Position.X);
    }

    [Fact]
    public void ConstructsAndComposesASnippetGivenOnlyItsType()
    {
        Block root = LayoutHarness.Build(frame => frame.Snippet<CaptionSnippet>());

        Assert.Equal(CaptionSnippet.DefaultCaption, LayoutHarness.Render(root, Space).Content);
    }

    [Fact]
    public void RefusesASnippetComposedTwiceIntoOneSlot()
    {
        Frame frame = new Frame();
        frame.Snippet(new CaptionSnippet("first"));

        Assert.Throws<CompositionException>(() => frame.Snippet(new CaptionSnippet("second")));
        Assert.Equal("first", LayoutHarness.Render(frame, Space).Content);
    }

    [Fact]
    public void ComposeHandsTheSameFrameToTheCompositionFunction()
    {
        Frame frame = new Frame();
        IFrame? received = null;

        frame.Compose(inner => received = inner);

        Assert.Same(frame, received);
    }

    [Fact]
    public void RefusesToBlankAFilledFrame()
    {
        Frame frame = new Frame();
        frame.Text("already here");

        CompositionException exception = Assert.Throws<CompositionException>(() => frame.Blank());

        Assert.Contains("This frame already holds TextBlock, so it cannot be left blank", exception.Message);
        Assert.IsType<TextBlock>(frame.Slot().Child);
    }

    [Fact]
    public void RefusesToBlankAMissingFrame()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => FrameContent.Blank(null!));

        Assert.Equal("parent", exception.ParamName);
    }

    [Theory]
    [InlineData("Plain words")]
    [InlineData("")]
    [InlineData(null)]
    public void PlainTextIsTheParagraphOfOneUnstyledRun(string? text)
    {
        Frame plain = new Frame();
        Frame composed = new Frame();

        plain.Text(text!);
        composed.Text(paragraph => paragraph.Run(text!));

        TextBlock block = Assert.IsType<TextBlock>(plain.Slot().Child);
        TextBlock expected = Assert.IsType<TextBlock>(composed.Slot().Child);

        // Every property of the paragraph and of its one run is as composing it by hand would leave it.
        foreach (System.Reflection.PropertyInfo property in typeof(TextBlock).GetProperties().Where(p => p.Name != nameof(TextBlock.Runs) && p.GetIndexParameters().Length == 0))
            Assert.Equal(property.GetValue(expected), property.GetValue(block));

        TextRun run = Assert.Single(block.Runs);
        TextRun expectedRun = Assert.Single(expected.Runs);

        foreach (System.Reflection.PropertyInfo property in typeof(TextRun).GetProperties())
            Assert.Equal(property.GetValue(expectedRun), property.GetValue(run));
    }

    [Fact]
    public void PlainTextHoldsItsOneRunWithoutSpareRoom()
    {
        Frame frame = new Frame();

        frame.Text("Plain words");

        // A table of 10,000 rows sets 30,000 plain cells: each keeps room for exactly the one run it has.
        Assert.Equal(1, Assert.IsType<TextBlock>(frame.Slot().Child).Runs.Capacity);
    }

    [Fact]
    public void PlainTextRefusesAMissingOrFilledFrameAsComposedTextDoes()
    {
        Frame filled = new Frame();
        filled.Text("already here");

        Assert.Equal("parent", Assert.Throws<ArgumentNullException>(() => FrameContent.Text(null!, "words")).ParamName);
        Assert.Equal(
            Assert.Throws<CompositionException>(() => filled.Text(paragraph => paragraph.Run("words"))).Message,
            Assert.Throws<CompositionException>(() => filled.Text("words")).Message);
    }
}
