using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Drives blocks and documents through the layout engine with deterministic services attached.
/// </summary>
internal static class LayoutHarness
{
    public static ITypeMeasurer Measurer { get; } = new FakeTypeMeasurer();

    /// <summary>Composes a fragment and returns its root block, ready to be measured or drawn.</summary>
    public static Block Build(Action<IFrame> compose)
    {
        Frame frame = new Frame();
        compose(frame);
        return frame;
    }

    public static PlanContext Context(Pagination? page = null, TypeStyle? defaultStyle = null)
    {
        PlanContext context = new PlanContext(Measurer, page ?? new Pagination());

        if (defaultStyle is not null)
            context.DefaultType = defaultStyle;

        return context;
    }

    public static Fit Plan(Block block, Extent availableSpace, PlanContext? context = null) =>
        block.Plan(availableSpace, context ?? Context());

    public static Fit Plan(Action<IFrame> compose, Extent availableSpace) =>
        Plan(Build(compose), availableSpace);

    /// <summary>Draws a block onto a single synthetic page and returns everything it produced.</summary>
    public static RecordedPage Render(Block block, Extent availableSpace, PlanContext? context = null)
    {
        RecordingSurface surface = new RecordingSurface();
        PlanContext layout = context ?? Context();

        surface.BeginPage(availableSpace);
        block.Render(availableSpace, new RenderContext(surface, layout));

        // Drawing must leave the surface exactly as it found it. A block that translates without translating
        // back, or saves without restoring, shifts every sibling drawn after it — invisible to a test that draws
        // one block and asserts one position, but wrong on every real page.
        Assert.True(surface.IsAtIdentity, $"{block.GetType().Name}.Render left the transform displaced.");
        Assert.True(surface.PendingSaves == 0, $"{block.GetType().Name}.Render left {surface.PendingSaves} unmatched Save call(s).");

        surface.EndPage();

        return surface.Pages[0];
    }

    public static RecordedPage Render(Action<IFrame> compose, Extent availableSpace) =>
        Render(Build(compose), availableSpace);

    /// <summary>
    /// Draws composed content tagged, on as many pages as it takes, and returns the document element it was drawn in and
    /// the pages.
    /// </summary>
    public static (StructureElement Root, RecordingSurface Pages) RenderTagged(Action<IFrame> compose, Extent availableSpace, int pages = 1)
    {
        Block block = Build(compose);
        RecordingSurface surface = new RecordingSurface();
        StructureElement root = new StructureElement("Document", null);
        RenderContext context = new RenderContext(surface, Context(), root);

        for (int page = 0; page < pages; page++)
        {
            surface.BeginPage(availableSpace);
            block.Render(availableSpace, context);
            surface.EndPage();
        }

        Assert.Same(root, context.Tags.Current);
        return (root, surface);
    }

    /// <summary>Renders a whole document tagged, returning its structure and every page it produced.</summary>
    public static (StructureElement Root, RecordingSurface Pages) RenderTagged(Document document)
    {
        RecordingSurface surface = new RecordingSurface();
        Typesetter.Render(document, surface, Measurer, tagged: true);
        return (surface.Root!, surface);
    }

    /// <summary>Renders a whole document, returning every page it produced.</summary>
    public static RecordingSurface Render(Document document)
    {
        RecordingSurface surface = new RecordingSurface();
        Typesetter.Render(document, surface, Measurer);
        return surface;
    }
}
