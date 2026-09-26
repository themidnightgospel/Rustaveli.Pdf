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
    private static T Attach<T>(IFrame parent, T element) where T : Block =>
        Composition.Attach(parent, element);

    // ---- Padding -------------------------------------------------------------------------------------------

    public static IFrame Padding(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.All(value) });

    public static IFrame PaddingHorizontal(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Symmetric(value, 0) });

    public static IFrame PaddingVertical(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Symmetric(0, value) });

    public static IFrame PaddingLeft(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Zero.WithLeft(value) });

    public static IFrame PaddingRight(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Zero.WithRight(value) });

    public static IFrame PaddingTop(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Zero.WithTop(value) });

    public static IFrame PaddingBottom(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Padding = Sides.Zero.WithBottom(value) });

    // ---- Painting ------------------------------------------------------------------------------------------

    public static IFrame Background(this IFrame parent, Ink color) =>
        Attach(parent, new FillBlock { Color = color });

    public static IFrame Background(this IFrame parent, string hexColor) =>
        parent.Background(Ink.Hex(hexColor));

    public static IFrame Border(this IFrame parent, float width) =>
        Attach(parent, new StrokeBlock { Width = Sides.All(width) });

    public static IFrame BorderLeft(this IFrame parent, float width) =>
        Attach(parent, new StrokeBlock { Width = Sides.Zero.WithLeft(width) });

    public static IFrame BorderRight(this IFrame parent, float width) =>
        Attach(parent, new StrokeBlock { Width = Sides.Zero.WithRight(width) });

    public static IFrame BorderTop(this IFrame parent, float width) =>
        Attach(parent, new StrokeBlock { Width = Sides.Zero.WithTop(width) });

    public static IFrame BorderBottom(this IFrame parent, float width) =>
        Attach(parent, new StrokeBlock { Width = Sides.Zero.WithBottom(width) });

    /// <summary>
    /// Sets the colour of the nearest enclosing border. Must follow one of the border methods.
    /// </summary>
    public static IFrame BorderColor(this IFrame parent, Ink color)
    {
        if (parent is not StrokeBlock border)
            throw new InvalidOperationException("BorderColor must be applied directly after a Border method.");

        border.Color = color;
        return border;
    }

    public static IFrame BorderColor(this IFrame parent, string hexColor) =>
        parent.BorderColor(Ink.Hex(hexColor));

    /// <summary>
    /// Rounds the corners of the nearest enclosing background or border. Must follow one of those methods.
    /// </summary>
    public static IFrame CornerRadius(this IFrame parent, float radius) => parent switch
    {
        FillBlock background => Assign(background, radius),
        StrokeBlock border => Assign(border, radius),
        _ => throw new InvalidOperationException("CornerRadius must be applied directly after a Background or Border method.")
    };

    private static IFrame Assign(FillBlock background, float radius)
    {
        background.CornerRadius = radius;
        return background;
    }

    private static IFrame Assign(StrokeBlock border, float radius)
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

    public static IFrame Width(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MinWidth = value, MaxWidth = value });

    public static IFrame MinWidth(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MinWidth = value });

    public static IFrame MaxWidth(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MaxWidth = value });

    public static IFrame Height(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MinHeight = value, MaxHeight = value });

    public static IFrame MinHeight(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MinHeight = value });

    public static IFrame MaxHeight(this IFrame parent, float value) =>
        Attach(parent, new ConstraintBlock { MaxHeight = value });

    public static IFrame Extend(this IFrame parent) =>
        Attach(parent, new ExpandBlock { ExtendHorizontal = true, ExtendVertical = true });

    public static IFrame ExtendHorizontal(this IFrame parent) =>
        Attach(parent, new ExpandBlock { ExtendHorizontal = true });

    public static IFrame ExtendVertical(this IFrame parent) =>
        Attach(parent, new ExpandBlock { ExtendVertical = true });

    public static IFrame AspectRatio(this IFrame parent, float ratio, ProportionFit option = ProportionFit.FitWidth) =>
        Attach(parent, new ProportionBlock { Ratio = ratio, Option = option });

    /// <summary>Shrinks the content just enough to fit the space available.</summary>
    public static IFrame ScaleToFit(this IFrame parent, float minScale = 0.25f) =>
        Attach(parent, new ShrinkToFitBlock { MinScale = minScale });

    /// <summary>Mirrors the content left to right.</summary>
    public static IFrame FlipHorizontal(this IFrame parent) =>
        Attach(parent, new MirrorBlock { FlipHorizontal = true });

    /// <summary>Mirrors the content top to bottom.</summary>
    public static IFrame FlipVertical(this IFrame parent) =>
        Attach(parent, new MirrorBlock { FlipVertical = true });

    /// <summary>Mirrors the content on both axes, equivalent to a half turn.</summary>
    public static IFrame FlipOver(this IFrame parent) =>
        Attach(parent, new MirrorBlock { FlipHorizontal = true, FlipVertical = true });

    // ---- Alignment -----------------------------------------------------------------------------------------

    public static IFrame AlignLeft(this IFrame parent) => Align(parent, horizontal: HorizontalPlacement.Left);

    public static IFrame AlignCenter(this IFrame parent) => Align(parent, horizontal: HorizontalPlacement.Center);

    public static IFrame AlignRight(this IFrame parent) => Align(parent, horizontal: HorizontalPlacement.Right);

    public static IFrame AlignTop(this IFrame parent) => Align(parent, vertical: VerticalPlacement.Top);

    public static IFrame AlignMiddle(this IFrame parent) => Align(parent, vertical: VerticalPlacement.Middle);

    public static IFrame AlignBottom(this IFrame parent) => Align(parent, vertical: VerticalPlacement.Bottom);

    /// <summary>
    /// Reuses an adjacent alignment element when one is already present, so that <c>.AlignRight().AlignMiddle()</c>
    /// aligns on both axes instead of nesting two elements that each claim the full space.
    /// </summary>
    private static IFrame Align(IFrame parent, HorizontalPlacement? horizontal = null, VerticalPlacement? vertical = null)
    {
        // Only fold into an aligner that is still empty. Once it has content, `.AlignRight()` on the same slot
        // is a second, separate piece of composition and must not quietly replace the first.
        if (parent is PlacementBlock existing && existing.Child is null)
        {
            existing.Horizontal = horizontal ?? existing.Horizontal;
            existing.Vertical = vertical ?? existing.Vertical;
            return existing;
        }

        return Attach(parent, new PlacementBlock { Horizontal = horizontal, Vertical = vertical });
    }

    // ---- Transforms ----------------------------------------------------------------------------------------

    public static IFrame TranslateX(this IFrame parent, float value) =>
        Attach(parent, new ShiftBlock { Offset = new Offset(value, 0) });

    public static IFrame TranslateY(this IFrame parent, float value) =>
        Attach(parent, new ShiftBlock { Offset = new Offset(0, value) });

    public static IFrame Scale(this IFrame parent, float factor) =>
        Attach(parent, new ScaleBlock { ScaleX = factor, ScaleY = factor });

    public static IFrame Scale(this IFrame parent, float scaleX, float scaleY) =>
        Attach(parent, new ScaleBlock { ScaleX = scaleX, ScaleY = scaleY });

    /// <summary>Rotates a quarter turn anticlockwise, swapping the layout axes.</summary>
    public static IFrame RotateLeft(this IFrame parent) =>
        Attach(parent, new TurnBlock { QuarterTurns = 3 });

    /// <summary>Rotates a quarter turn clockwise, swapping the layout axes.</summary>
    public static IFrame RotateRight(this IFrame parent) =>
        Attach(parent, new TurnBlock { QuarterTurns = 1 });

    // ---- Flow control --------------------------------------------------------------------------------------

    public static IFrame ShowIf(this IFrame parent, bool condition) =>
        Attach(parent, new WhenBlock { Condition = condition });

    public static IFrame ShowOnce(this IFrame parent) =>
        Attach(parent, new OnceBlock());

    public static IFrame SkipOnce(this IFrame parent) =>
        Attach(parent, new SkipFirstBlock());

    public static void PageBreak(this IFrame parent) =>
        Attach(parent, new NewPageBlock());

    // ---- Inherited context ---------------------------------------------------------------------------------

    /// <summary>Sets the flow direction for everything nested inside.</summary>
    public static IFrame ContentFrom(this IFrame parent, ReadingDirection direction) =>
        Attach(parent, new ReadingDirectionBlock { Direction = direction });

    /// <summary>Lays out nested content right to left.</summary>
    public static IFrame RightToLeft(this IFrame parent) =>
        parent.ContentFrom(ReadingDirection.RightToLeft);

    /// <summary>Lays out nested content left to right.</summary>
    public static IFrame LeftToRight(this IFrame parent) =>
        parent.ContentFrom(ReadingDirection.LeftToRight);

    /// <summary>Adjusts the style inherited by all text nested inside.</summary>
    public static IFrame DefaultTextStyle(this IFrame parent, Func<TypeStyle, TypeStyle> refinement)
    {
        ArgumentNullException.ThrowIfNull(refinement);
        return Attach(parent, new DefaultTypeBlock { Refinement = refinement });
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    /// <summary>
    /// Lets content exceed the space offered to it while reporting no size to its parent.
    /// </summary>
    public static IFrame Unconstrained(this IFrame parent) =>
        Attach(parent, new UnboundedBlock());

    /// <summary>
    /// Prevents content from being split across pages, moving it whole to the next page instead.
    /// </summary>
    public static IFrame ShowEntire(this IFrame parent) =>
        Attach(parent, new KeepTogetherBlock());

    /// <summary>
    /// Defers the content to the next page unless at least <paramref name="minHeight"/> remains, so a heading
    /// or short block is never stranded at the bottom of a page.
    /// </summary>
    public static IFrame EnsureSpace(this IFrame parent, float minHeight) =>
        Attach(parent, new RequireSpaceBlock { MinHeight = minHeight });

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    /// <summary>Draws a horizontal rule across the available width.</summary>
    public static void LineHorizontal(this IFrame parent, float thickness = 1f, Ink? color = null) =>
        Attach(parent, new RuleBlock { Thickness = thickness, Color = color ?? Ink.Black });

    /// <summary>Draws a vertical rule down the available height.</summary>
    public static void LineVertical(this IFrame parent, float thickness = 1f, Ink? color = null) =>
        Attach(parent, new VerticalRuleBlock { Thickness = thickness, Color = color ?? Ink.Black });

    /// <summary>Fills the available space with a block standing in for unwritten content.</summary>
    public static void Placeholder(this IFrame parent, Ink? color = null) =>
        Attach(parent, new PlaceholderBlock { Color = color ?? Ink.Rgb(0xEE, 0xEE, 0xEE) });

    // ---- Links ---------------------------------------------------------------------------------------------

    public static IFrame Hyperlink(this IFrame parent, string url)
    {
        // An empty target makes the element draw no annotation at all, so the region would look linked in the
        // source and do nothing in the file.
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return Attach(parent, new LinkBlock { Url = url });
    }

    /// <summary>Marks this content as a named destination that <see cref="SectionLink"/> can target.</summary>
    public static IFrame Section(this IFrame parent, string name)
    {
        // An unnamed section registers no destination, so every link and page reference aimed at it would
        // silently resolve to nothing for the life of the document.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Attach(parent, new AnchorBlock { Name = name });
    }

    public static IFrame SectionLink(this IFrame parent, string sectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return Attach(parent, new CrossReferenceBlock { DestinationName = sectionName });
    }
}
