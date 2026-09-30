using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

public static class FrameContent
{
    /// <summary>Adds a paragraph of styled text.</summary>
    public static void Text(this IFrame parent, Action<TextComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        TextBlock element = FrameAttachment.Attach(parent, new TextBlock());
        handler(new TextComposer(element));
    }

    public static void Text(this IFrame parent, string text) => throw new NotImplementedException("To be written anew from its specification.");

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

    /// <summary>
    /// Adds an image generated for the box it fills, taking all the room there is, at the resolution images are
    /// generated at: a chart drawn by another library, say. Returning nothing leaves the box empty.
    /// </summary>
    public static void Image(this IFrame parent, Func<ImageRequest, byte[]?> generate)
    {
        ArgumentNullException.ThrowIfNull(generate);
        FrameAttachment.Attach(parent, new GeneratedImageBlock { Generate = generate });
    }

    /// <summary>
    /// Adds artwork generated for the box it fills, taking all the room there is — an SVG written for exactly that
    /// size, say — stretched to fill it. Returning nothing leaves the box empty.
    /// </summary>
    public static void Artwork(this IFrame parent, Func<Extent, Artwork?> generate)
    {
        ArgumentNullException.ThrowIfNull(generate);
        FrameAttachment.Attach(parent, new GeneratedArtworkBlock { Generate = generate });
    }

    /// <summary>Adds vector artwork, scaled to the frame according to <paramref name="fit"/> and kept vector in the PDF.</summary>
    public static void Artwork(this IFrame parent, Artwork artwork, ImageFitting fit = ImageFitting.FitWidth)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        FrameAttachment.Attach(parent, new ArtworkBlock { Artwork = artwork, Fit = fit });
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

    public static void Table(this IFrame parent, Action<TableComposer> handler) => throw new NotImplementedException("To be written anew from its specification.");

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

    public static void Compose(this IFrame parent, Action<IFrame> handler) => throw new NotImplementedException("To be written anew from its specification.");

    /// <summary>
    /// Composes the frame's content only when layout first reaches it, and lets it go once it is drawn in full, so a
    /// very large document holds only the content of the pages being drawn. Kept, the content is composed once and
    /// held instead, which is quicker when the same content is laid out again in every pass over the document.
    /// </summary>
    public static void ComposeLater(this IFrame parent, Action<IFrame> handler, bool keep = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FrameAttachment.Attach(parent, new LaterBlock { Compose = handler, Keep = keep });
    }

    /// <summary>
    /// Composes the frame's content afresh for every page it reaches, knowing the page and the room left on it, from
    /// the state it got to on the page before.
    /// </summary>
    public static void ComposePerPage<TState>(this IFrame parent, IDynamicContent<TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        FrameAttachment.Attach(parent, new DynamicBlock<TState>(content));
    }

    public static void Snippet(this IFrame parent, ISnippet snippet) => throw new NotImplementedException("To be written anew from its specification.");

    public static void Snippet<T>(this IFrame parent) where T : ISnippet, new() => throw new NotImplementedException("To be written anew from its specification.");

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
