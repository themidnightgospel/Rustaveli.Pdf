using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Composition methods that place content into a container.
/// </summary>
public static class FrameContent
{
    /// <summary>Adds a paragraph of styled text.</summary>
    public static void Text(this IFrame parent, Action<TextComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        TextBlock element = FrameAttachment.Attach(parent, new TextBlock());
        handler(new TextComposer(element));
    }

    /// <summary>Adds a paragraph consisting of a single unstyled run.</summary>
    public static void Text(this IFrame parent, string text)
    {
        parent.Text(descriptor => descriptor.Run(text));
    }

    /// <summary>Adds an image scaled according to <paramref name="fit" />.</summary>
    public static void Image(this IFrame parent, IImage image, ImageFitting fit = ImageFitting.FitWidth)
    {
        ArgumentNullException.ThrowIfNull(image);
        FrameAttachment.Attach(parent, new ImageBlock
        {
            Image = image,
            Fit = fit
        });
    }

    /// <summary>Stacks content vertically, flowing across pages when it does not fit.</summary>
    public static void Stack(this IFrame parent, Action<StackComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        StackBlock element = FrameAttachment.Attach(parent, new StackBlock());
        handler(new StackComposer(element));
    }

    /// <summary>Places content side by side.</summary>
    public static void Columns(this IFrame parent, Action<ColumnsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ColumnsBlock element = FrameAttachment.Attach(parent, new ColumnsBlock());
        handler(new ColumnsComposer(element));
    }

    /// <summary>
    /// Adds columns a story flows through as a newspaper's does: down one, on into the next, and on to the next page.
    /// </summary>
    public static void FlowColumns(this IFrame parent, Action<FlowColumnsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FlowColumnsBlock element = FrameAttachment.Attach(parent, new FlowColumnsBlock());
        handler(new FlowColumnsComposer(element));
    }

    /// <summary>
    /// Adds a flow of items set side by side as words are, wrapping on to a new line wherever the next would not fit.
    /// </summary>
    public static void Flow(this IFrame parent, Action<FlowComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FlowBlock element = FrameAttachment.Attach(parent, new FlowBlock());
        handler(new FlowComposer(element));
    }

    /// <summary>
    /// Adds a grid of cells flowing into rows of equal columns, each cell spanning one or more.
    /// </summary>
    public static void Grid(this IFrame parent, Action<GridComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        StackBlock element = FrameAttachment.Attach(parent, new StackBlock());
        GridComposer grid = new GridComposer();
        handler(grid);
        grid.Build(element);
    }

    /// <summary>Adds a grid with sized columns and optional repeating bands.</summary>
    public static void Table(this IFrame parent, Action<TableComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        TableBlock element = FrameAttachment.Attach(parent, new TableBlock());
        TableComposer tableDescriptor = new TableComposer(element);
        handler(tableDescriptor);
        tableDescriptor.PlaceAutomaticCells();
    }

    /// <summary>Adds a bulleted or numbered list.</summary>
    public static void List(this IFrame parent, Action<ListComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ListBlock listElement = FrameAttachment.Attach(parent, new ListBlock());
        handler(new ListComposer(listElement));
        listElement.Build();
    }

    /// <summary>Draws content in overlapping layers.</summary>
    public static void Layered(this IFrame parent, Action<LayersComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        LayersBlock element = FrameAttachment.Attach(parent, new LayersBlock());
        handler(new LayersComposer(element));
    }

    /// <summary>Adds flowing content framed by bands that repeat on every page.</summary>
    public static void Banded(this IFrame parent, Action<BandsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        BandsBlock element = FrameAttachment.Attach(parent, new BandsBlock());
        handler(new BandsComposer(element));
    }

    /// <summary>
    /// Applies a composition function, letting shared layout be factored into an ordinary method.
    /// </summary>
    public static void Compose(this IFrame parent, Action<IFrame> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(parent);
    }

    /// <summary>Composes a reusable component into this container.</summary>
    public static void Snippet(this IFrame parent, ISnippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        snippet.Compose(parent);
    }

    /// <summary>Composes a reusable component into this container.</summary>
    public static void Snippet<T>(this IFrame parent) where T : ISnippet, new()
    {
        parent.Snippet(new T());
    }

    /// <summary>
    /// Marks the container as deliberately blank.
    /// </summary>
    /// <remarks>
    /// Refuses a container that already holds content. Blanking it would discard a whole subtree with no
    /// diagnostic — the very thing <see cref="FrameAttachment.Attach{T}"/> exists to prevent.
    /// </remarks>
    public static void Blank(this IFrame parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Block? existing = FrameAttachment.Slot(parent).Child;
        if (existing != null)
        {
            throw new CompositionException("This frame already holds " + existing.GetType().Name + ", so it cannot be left blank. Blank states that nothing was ever placed here; it does not remove existing content.");
        }
    }
}
