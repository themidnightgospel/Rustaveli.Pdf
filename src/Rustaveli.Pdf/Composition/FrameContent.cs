using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// The content a frame can hold: text, pictures, and the arrangements — stacks, rows, grids, tables and more — that
/// hold further frames of their own.
/// </summary>
/// <remarks>
/// Each method fills the frame it is called on and returns nothing, because content is where a chain of modifiers
/// ends. A frame holds one piece of content, so filling it a second time fails.
/// </remarks>
public static class FrameContent
{
    /// <summary>Adds a paragraph of styled text.</summary>
    public static void Text(this IFrame parent, Action<TextComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        TextBlock block = FrameAttachment.Attach(parent, new TextBlock());
        handler(new TextComposer(block));
    }

    /// <summary>
    /// Adds a paragraph of plain text, styled only by the style it inherits from around it: the same as a paragraph of
    /// one run of <paramref name="text"/> with nothing set on it.
    /// </summary>
    public static void Text(this IFrame parent, string text) => parent.Text(paragraph => paragraph.Run(text));

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
        StackBlock block = FrameAttachment.Attach(parent, new StackBlock());
        handler(new StackComposer(block));
    }

    /// <summary>Places content side by side.</summary>
    public static void Columns(this IFrame parent, Action<ColumnsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ColumnsBlock block = FrameAttachment.Attach(parent, new ColumnsBlock());
        handler(new ColumnsComposer(block));
    }

    /// <summary>
    /// Adds columns a story flows through as a newspaper's does: down one, on into the next, and on to the next page.
    /// </summary>
    public static void FlowColumns(this IFrame parent, Action<FlowColumnsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FlowColumnsBlock block = FrameAttachment.Attach(parent, new FlowColumnsBlock());
        handler(new FlowColumnsComposer(block));
    }

    /// <summary>
    /// Adds a flow of items set side by side as words are, wrapping on to a new line wherever the next would not fit.
    /// </summary>
    public static void Flow(this IFrame parent, Action<FlowComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FlowBlock block = FrameAttachment.Attach(parent, new FlowBlock());
        handler(new FlowComposer(block));
    }

    /// <summary>
    /// Adds a grid of cells flowing into rows of equal columns, each cell spanning one or more.
    /// </summary>
    public static void Grid(this IFrame parent, Action<GridComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        StackBlock block = FrameAttachment.Attach(parent, new StackBlock());
        GridComposer grid = new GridComposer();
        handler(grid);
        grid.Build(block);
    }

    /// <summary>
    /// Adds a table of cells in declared columns, which runs on to the next page between rows. Its cells are placed
    /// and checked once <paramref name="handler"/> returns, so a table that cannot be laid out fails here.
    /// </summary>
    public static void Table(this IFrame parent, Action<TableComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        TableBlock table = FrameAttachment.Attach(parent, new TableBlock());
        TableComposer composer = new TableComposer(table);
        handler(composer);
        composer.PlaceAutomaticCells();
    }

    /// <summary>Adds a bulleted or numbered list.</summary>
    public static void List(this IFrame parent, Action<ListComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ListBlock list = FrameAttachment.Attach(parent, new ListBlock());
        handler(new ListComposer(list));
        list.Build();
    }

    /// <summary>Draws content in overlapping layers.</summary>
    public static void Layered(this IFrame parent, Action<LayersComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        LayersBlock block = FrameAttachment.Attach(parent, new LayersBlock());
        handler(new LayersComposer(block));
    }

    /// <summary>Adds flowing content framed by bands that repeat on every page.</summary>
    public static void Banded(this IFrame parent, Action<BandsComposer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        BandsBlock block = FrameAttachment.Attach(parent, new BandsBlock());
        handler(new BandsComposer(block));
    }

    /// <summary>
    /// Hands the frame itself to <paramref name="handler"/>, which fills it: a step of composing written as a method of
    /// its own and applied in the middle of a chain. This places nothing itself.
    /// </summary>
    public static void Compose(this IFrame parent, Action<IFrame> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(parent);
    }

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

    /// <summary>
    /// Has <paramref name="snippet"/> compose its content into this frame, exactly as if its composing were written out
    /// here: it adds no frame, room or structure of its own, so the modifiers before it apply to its content as they
    /// would to anything else.
    /// </summary>
    public static void Snippet(this IFrame parent, ISnippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        snippet.Compose(parent);
    }

    /// <summary>Makes a new <typeparamref name="T"/> and has it compose its content into this frame.</summary>
    public static void Snippet<T>(this IFrame parent) where T : ISnippet, new() => parent.Snippet(new T());

    /// <summary>
    /// States that the frame is left empty on purpose, so that code reading as incomplete says what it means. It
    /// changes nothing, and fails for a frame that already holds content, which it would not remove.
    /// </summary>
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
