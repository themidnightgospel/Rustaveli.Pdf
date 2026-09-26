using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Composition methods that place content into a container.
/// </summary>
public static class ContentExtensions
{
    /// <summary>Adds a paragraph of styled text.</summary>
    public static void Text(this IFrame parent, Action<TextDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        TextBlock element = Composition.Attach(parent, new TextBlock());
        handler(new TextDescriptor(element));
    }

    /// <summary>Adds a paragraph consisting of a single unstyled run.</summary>
    public static void Text(this IFrame parent, string text)
    {
        parent.Text(descriptor => descriptor.Span(text));
    }

    /// <summary>Adds an image scaled according to <paramref name="fit" />.</summary>
    public static void Image(this IFrame parent, IImage image, ImageFitting fit = ImageFitting.Width)
    {
        ArgumentNullException.ThrowIfNull(image, "image");
        Composition.Attach(parent, new ImageBlock
        {
            Image = image,
            Fit = fit
        });
    }

    /// <summary>Stacks content vertically, flowing across pages when it does not fit.</summary>
    public static void Column(this IFrame parent, Action<ColumnDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        StackBlock element = Composition.Attach(parent, new StackBlock());
        handler(new ColumnDescriptor(element));
    }

    /// <summary>Places content side by side.</summary>
    public static void Row(this IFrame parent, Action<RowDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        ColumnsBlock element = Composition.Attach(parent, new ColumnsBlock());
        handler(new RowDescriptor(element));
    }

    /// <summary>Adds a grid with sized columns and optional repeating bands.</summary>
    public static void Table(this IFrame parent, Action<TableDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        TableElement element = Composition.Attach(parent, new TableElement());
        TableDescriptor tableDescriptor = new TableDescriptor(element);
        handler(tableDescriptor);
        tableDescriptor.PlaceAutomaticCells();
    }

    /// <summary>Adds a bulleted or numbered list.</summary>
    public static void List(this IFrame parent, Action<ListDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        ListBlock listElement = Composition.Attach(parent, new ListBlock());
        handler(new ListDescriptor(listElement));
        listElement.Build();
    }

    /// <summary>Draws content in overlapping layers.</summary>
    public static void Layers(this IFrame parent, Action<LayersDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        LayersBlock element = Composition.Attach(parent, new LayersBlock());
        handler(new LayersDescriptor(element));
    }

    /// <summary>Adds flowing content framed by bands that repeat on every page.</summary>
    public static void Decoration(this IFrame parent, Action<DecorationDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        BandsBlock element = Composition.Attach(parent, new BandsBlock());
        handler(new DecorationDescriptor(element));
    }

    /// <summary>
    /// Applies a composition function, letting shared layout be factored into an ordinary method.
    /// </summary>
    public static void Element(this IFrame parent, Action<IFrame> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(parent);
    }

    /// <summary>Composes a reusable component into this container.</summary>
    public static void Component(this IFrame parent, IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component, "component");
        component.Compose(parent);
    }

    /// <summary>Composes a reusable component into this container.</summary>
    public static void Component<T>(this IFrame parent) where T : IComponent, new()
    {
        parent.Component(new T());
    }

    /// <summary>
    /// Marks the container as deliberately blank.
    /// </summary>
    /// <remarks>
    /// Refuses a container that already holds content. Blanking it would discard a whole subtree with no
    /// diagnostic — the very thing <see cref="Composition.Attach{T}"/> exists to prevent.
    /// </remarks>
    public static void Empty(this IFrame parent)
    {
        ArgumentNullException.ThrowIfNull(parent, "parent");
        if (parent.Child != null)
        {
            throw new CompositionException("This container already holds " + parent.Child.GetType().Name + ", so it cannot be marked empty. Empty states that nothing was ever placed here; it does not remove existing content.");
        }
    }
}
