namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Drives elements and documents through the layout engine with deterministic services attached.
/// </summary>
public static class LayoutHarness
{
    public static ITextMeasurer Measurer { get; } = new FakeTextMeasurer();

    /// <summary>Composes a fragment and returns its root element, ready to be measured or drawn.</summary>
    public static Element Build(Action<IContainer> compose)
    {
        Container container = new Container();
        compose(container);
        return container;
    }

    public static LayoutContext Context(PageContext? page = null, TextStyle? defaultStyle = null)
    {
        LayoutContext context = new LayoutContext(Measurer, page ?? new PageContext());

        if (defaultStyle is not null)
            context.DefaultTextStyle = defaultStyle;

        return context;
    }

    public static SpacePlan Measure(Element element, Size availableSpace, LayoutContext? context = null) =>
        element.Measure(availableSpace, context ?? Context());

    public static SpacePlan Measure(Action<IContainer> compose, Size availableSpace) =>
        Measure(Build(compose), availableSpace);

    /// <summary>Draws an element onto a single synthetic page and returns everything it produced.</summary>
    public static RecordedPage Draw(Element element, Size availableSpace, LayoutContext? context = null)
    {
        RecordingCanvas canvas = new RecordingCanvas();
        LayoutContext layout = context ?? Context();

        canvas.BeginPage(availableSpace);
        element.Draw(availableSpace, new DrawContext(canvas, layout));

        // Drawing must leave the canvas exactly as it found it. An element that translates without translating
        // back, or saves without restoring, shifts every sibling drawn after it — invisible to a test that draws
        // one element and asserts one position, but wrong on every real page.
        Assert.True(canvas.IsAtIdentity, $"{element.GetType().Name}.Draw left the transform displaced.");
        Assert.True(canvas.PendingSaves == 0, $"{element.GetType().Name}.Draw left {canvas.PendingSaves} unmatched Save call(s).");

        canvas.EndPage();

        return canvas.Pages[0];
    }

    public static RecordedPage Draw(Action<IContainer> compose, Size availableSpace) =>
        Draw(Build(compose), availableSpace);

    /// <summary>Renders a whole document, returning every page it produced.</summary>
    public static RecordingCanvas Render(Document document)
    {
        RecordingCanvas canvas = new RecordingCanvas();
        DocumentRenderer.Render(document, canvas, Measurer);
        return canvas;
    }
}
