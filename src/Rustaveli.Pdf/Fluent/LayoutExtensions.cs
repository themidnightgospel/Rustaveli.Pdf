using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Composition methods for spacing, sizing, alignment and flow control.
/// </summary>
/// <remarks>
/// Most methods attach one element to the container they are called on and return that element as the next
/// container, so a chain such as <c>.Padding(10).Background(...)</c> nests rather than accumulating flags. The
/// exceptions are the configurators — <c>BorderColor</c> and <c>CornerRadius</c> — which attach nothing and
/// return the element they just adjusted, and the alignment methods, which fold into an adjacent empty aligner.
/// </remarks>
public static class LayoutExtensions
{
    private static T Attach<T>(IContainer parent, T element) where T : Element =>
        Composition.Attach(parent, element);

    // ---- Padding -------------------------------------------------------------------------------------------

    public static IContainer Padding(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.All(value) });

    public static IContainer PaddingHorizontal(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Symmetric(value, 0) });

    public static IContainer PaddingVertical(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Symmetric(0, value) });

    public static IContainer PaddingLeft(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Zero.WithLeft(value) });

    public static IContainer PaddingRight(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Zero.WithRight(value) });

    public static IContainer PaddingTop(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Zero.WithTop(value) });

    public static IContainer PaddingBottom(this IContainer parent, float value) =>
        Attach(parent, new PaddingElement { Padding = Edges.Zero.WithBottom(value) });

    // ---- Painting ------------------------------------------------------------------------------------------

    public static IContainer Background(this IContainer parent, Color color) =>
        Attach(parent, new BackgroundElement { Color = color });

    public static IContainer Background(this IContainer parent, string hexColor) =>
        parent.Background(Color.ParseHex(hexColor));

    public static IContainer Border(this IContainer parent, float width) =>
        Attach(parent, new BorderElement { Width = Edges.All(width) });

    public static IContainer BorderLeft(this IContainer parent, float width) =>
        Attach(parent, new BorderElement { Width = Edges.Zero.WithLeft(width) });

    public static IContainer BorderRight(this IContainer parent, float width) =>
        Attach(parent, new BorderElement { Width = Edges.Zero.WithRight(width) });

    public static IContainer BorderTop(this IContainer parent, float width) =>
        Attach(parent, new BorderElement { Width = Edges.Zero.WithTop(width) });

    public static IContainer BorderBottom(this IContainer parent, float width) =>
        Attach(parent, new BorderElement { Width = Edges.Zero.WithBottom(width) });

    /// <summary>
    /// Sets the colour of the nearest enclosing border. Must follow one of the border methods.
    /// </summary>
    public static IContainer BorderColor(this IContainer parent, Color color)
    {
        if (parent is not BorderElement border)
            throw new InvalidOperationException("BorderColor must be applied directly after a Border method.");

        border.Color = color;
        return border;
    }

    public static IContainer BorderColor(this IContainer parent, string hexColor) =>
        parent.BorderColor(Color.ParseHex(hexColor));

    /// <summary>
    /// Rounds the corners of the nearest enclosing background or border. Must follow one of those methods.
    /// </summary>
    public static IContainer CornerRadius(this IContainer parent, float radius) => parent switch
    {
        BackgroundElement background => Assign(background, radius),
        BorderElement border => Assign(border, radius),
        _ => throw new InvalidOperationException("CornerRadius must be applied directly after a Background or Border method.")
    };

    private static IContainer Assign(BackgroundElement background, float radius)
    {
        background.CornerRadius = radius;
        return background;
    }

    private static IContainer Assign(BorderElement border, float radius)
    {
        // A rounded corner has no shape where two different thicknesses meet, so the element ignores the radius
        // unless every side matches. Saying so here beats accepting the call and quietly drawing square corners.
        if (radius > 0 && !border.HasUniformWidth)
        {
            throw new InvalidOperationException(
                "CornerRadius requires a border of uniform width. Use Border(width) rather than a single-sided " +
                "BorderLeft/Right/Top/Bottom, and give it a width greater than zero.");
        }

        border.CornerRadius = radius;
        return border;
    }

    // ---- Sizing --------------------------------------------------------------------------------------------

    public static IContainer Width(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MinWidth = value, MaxWidth = value });

    public static IContainer MinWidth(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MinWidth = value });

    public static IContainer MaxWidth(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MaxWidth = value });

    public static IContainer Height(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MinHeight = value, MaxHeight = value });

    public static IContainer MinHeight(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MinHeight = value });

    public static IContainer MaxHeight(this IContainer parent, float value) =>
        Attach(parent, new ConstrainedElement { MaxHeight = value });

    public static IContainer Extend(this IContainer parent) =>
        Attach(parent, new ExtendElement { ExtendHorizontal = true, ExtendVertical = true });

    public static IContainer ExtendHorizontal(this IContainer parent) =>
        Attach(parent, new ExtendElement { ExtendHorizontal = true });

    public static IContainer ExtendVertical(this IContainer parent) =>
        Attach(parent, new ExtendElement { ExtendVertical = true });

    public static IContainer AspectRatio(this IContainer parent, float ratio, AspectRatioOption option = AspectRatioOption.FitWidth) =>
        Attach(parent, new AspectRatioElement { Ratio = ratio, Option = option });

    /// <summary>Shrinks the content just enough to fit the space available.</summary>
    public static IContainer ScaleToFit(this IContainer parent, float minScale = 0.25f) =>
        Attach(parent, new ScaleToFitElement { MinScale = minScale });

    /// <summary>Mirrors the content left to right.</summary>
    public static IContainer FlipHorizontal(this IContainer parent) =>
        Attach(parent, new FlipElement { FlipHorizontal = true });

    /// <summary>Mirrors the content top to bottom.</summary>
    public static IContainer FlipVertical(this IContainer parent) =>
        Attach(parent, new FlipElement { FlipVertical = true });

    /// <summary>Mirrors the content on both axes, equivalent to a half turn.</summary>
    public static IContainer FlipOver(this IContainer parent) =>
        Attach(parent, new FlipElement { FlipHorizontal = true, FlipVertical = true });

    // ---- Alignment -----------------------------------------------------------------------------------------

    public static IContainer AlignLeft(this IContainer parent) => Align(parent, horizontal: HorizontalAlignment.Left);

    public static IContainer AlignCenter(this IContainer parent) => Align(parent, horizontal: HorizontalAlignment.Center);

    public static IContainer AlignRight(this IContainer parent) => Align(parent, horizontal: HorizontalAlignment.Right);

    public static IContainer AlignTop(this IContainer parent) => Align(parent, vertical: VerticalAlignment.Top);

    public static IContainer AlignMiddle(this IContainer parent) => Align(parent, vertical: VerticalAlignment.Middle);

    public static IContainer AlignBottom(this IContainer parent) => Align(parent, vertical: VerticalAlignment.Bottom);

    /// <summary>
    /// Reuses an adjacent alignment element when one is already present, so that <c>.AlignRight().AlignMiddle()</c>
    /// aligns on both axes instead of nesting two elements that each claim the full space.
    /// </summary>
    private static IContainer Align(IContainer parent, HorizontalAlignment? horizontal = null, VerticalAlignment? vertical = null)
    {
        // Only fold into an aligner that is still empty. Once it has content, `.AlignRight()` on the same slot
        // is a second, separate piece of composition and must not quietly replace the first.
        if (parent is AlignmentElement existing && existing.Child is null)
        {
            existing.Horizontal = horizontal ?? existing.Horizontal;
            existing.Vertical = vertical ?? existing.Vertical;
            return existing;
        }

        return Attach(parent, new AlignmentElement { Horizontal = horizontal, Vertical = vertical });
    }

    // ---- Transforms ----------------------------------------------------------------------------------------

    public static IContainer TranslateX(this IContainer parent, float value) =>
        Attach(parent, new TranslateElement { Offset = new Position(value, 0) });

    public static IContainer TranslateY(this IContainer parent, float value) =>
        Attach(parent, new TranslateElement { Offset = new Position(0, value) });

    public static IContainer Scale(this IContainer parent, float factor) =>
        Attach(parent, new ScaleElement { ScaleX = factor, ScaleY = factor });

    public static IContainer Scale(this IContainer parent, float scaleX, float scaleY) =>
        Attach(parent, new ScaleElement { ScaleX = scaleX, ScaleY = scaleY });

    /// <summary>Rotates a quarter turn anticlockwise, swapping the layout axes.</summary>
    public static IContainer RotateLeft(this IContainer parent) =>
        Attach(parent, new RotateElement { QuarterTurns = 3 });

    /// <summary>Rotates a quarter turn clockwise, swapping the layout axes.</summary>
    public static IContainer RotateRight(this IContainer parent) =>
        Attach(parent, new RotateElement { QuarterTurns = 1 });

    // ---- Flow control --------------------------------------------------------------------------------------

    public static IContainer ShowIf(this IContainer parent, bool condition) =>
        Attach(parent, new ShowIfElement { Condition = condition });

    public static IContainer ShowOnce(this IContainer parent) =>
        Attach(parent, new ShowOnceElement());

    public static IContainer SkipOnce(this IContainer parent) =>
        Attach(parent, new SkipOnceElement());

    public static void PageBreak(this IContainer parent) =>
        Attach(parent, new PageBreakElement());

    // ---- Inherited context ---------------------------------------------------------------------------------

    /// <summary>Sets the flow direction for everything nested inside.</summary>
    public static IContainer ContentFrom(this IContainer parent, ContentDirection direction) =>
        Attach(parent, new DirectionElement { Direction = direction });

    /// <summary>Lays out nested content right to left.</summary>
    public static IContainer RightToLeft(this IContainer parent) =>
        parent.ContentFrom(ContentDirection.RightToLeft);

    /// <summary>Lays out nested content left to right.</summary>
    public static IContainer LeftToRight(this IContainer parent) =>
        parent.ContentFrom(ContentDirection.LeftToRight);

    /// <summary>Adjusts the style inherited by all text nested inside.</summary>
    public static IContainer DefaultTextStyle(this IContainer parent, Func<TextStyle, TextStyle> refinement)
    {
        ArgumentNullException.ThrowIfNull(refinement);
        return Attach(parent, new DefaultTextStyleElement { Refinement = refinement });
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    /// <summary>
    /// Lets content exceed the space offered to it while reporting no size to its parent.
    /// </summary>
    public static IContainer Unconstrained(this IContainer parent) =>
        Attach(parent, new UnconstrainedElement());

    /// <summary>
    /// Prevents content from being split across pages, moving it whole to the next page instead.
    /// </summary>
    public static IContainer ShowEntire(this IContainer parent) =>
        Attach(parent, new ShowEntireElement());

    /// <summary>
    /// Defers the content to the next page unless at least <paramref name="minHeight"/> remains, so a heading
    /// or short block is never stranded at the bottom of a page.
    /// </summary>
    public static IContainer EnsureSpace(this IContainer parent, float minHeight) =>
        Attach(parent, new EnsureSpaceElement { MinHeight = minHeight });

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    /// <summary>Draws a horizontal rule across the available width.</summary>
    public static void LineHorizontal(this IContainer parent, float thickness = 1f, Color? color = null) =>
        Attach(parent, new HorizontalLineElement { Thickness = thickness, Color = color ?? Colors.Black });

    /// <summary>Draws a vertical rule down the available height.</summary>
    public static void LineVertical(this IContainer parent, float thickness = 1f, Color? color = null) =>
        Attach(parent, new VerticalLineElement { Thickness = thickness, Color = color ?? Colors.Black });

    /// <summary>Fills the available space with a block standing in for unwritten content.</summary>
    public static void Placeholder(this IContainer parent, Color? color = null) =>
        Attach(parent, new PlaceholderElement { Color = color ?? Colors.Grey.Lighten3 });

    // ---- Links ---------------------------------------------------------------------------------------------

    public static IContainer Hyperlink(this IContainer parent, string url)
    {
        // An empty target makes the element draw no annotation at all, so the region would look linked in the
        // source and do nothing in the file.
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return Attach(parent, new HyperlinkElement { Url = url });
    }

    /// <summary>Marks this content as a named destination that <see cref="SectionLink"/> can target.</summary>
    public static IContainer Section(this IContainer parent, string name)
    {
        // An unnamed section registers no destination, so every link and page reference aimed at it would
        // silently resolve to nothing for the life of the document.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Attach(parent, new SectionElement { Name = name });
    }

    public static IContainer SectionLink(this IContainer parent, string sectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return Attach(parent, new InternalLinkElement { DestinationName = sectionName });
    }
}
