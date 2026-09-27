using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Composition methods for spacing, sizing, alignment and flow control.
/// </summary>
/// <remarks>
/// Most methods attach one element to the container they are called on and return that element as the next
/// container, so a chain such as <c>.Inset(10).Fill(...)</c> nests rather than accumulating flags. The
/// exceptions are the configurators — <c>StrokeInk</c>, <c>AlignStroke</c> and <c>RoundCorners</c> — which attach
/// nothing and return the element they just adjusted, and the alignment methods, which fold into an adjacent empty
/// aligner.
/// </remarks>
public static class FrameModifiers
{
    private static T Attach<T>(IFrame parent, T element) where T : Block =>
        FrameAttachment.Attach(parent, element);

    // ---- Padding -------------------------------------------------------------------------------------------

    public static IFrame Inset(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.All(value) });

    public static IFrame InsetHorizontal(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Symmetric(value, 0) });

    public static IFrame InsetVertical(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Symmetric(0, value) });

    public static IFrame InsetLeft(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Zero.WithLeft(value) });

    public static IFrame InsetRight(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Zero.WithRight(value) });

    public static IFrame InsetTop(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Zero.WithTop(value) });

    public static IFrame InsetBottom(this IFrame parent, float value) =>
        Attach(parent, new InsetBlock { Inset = Sides.Zero.WithBottom(value) });

    // ---- Painting ------------------------------------------------------------------------------------------

    public static IFrame Fill(this IFrame parent, Ink ink) =>
        Attach(parent, new FillBlock { Ink = ink });

    public static IFrame Fill(this IFrame parent, string hex) =>
        parent.Fill(Ink.Hex(hex));

    /// <summary>Paints a gradient behind the content, across the whole frame.</summary>
    public static IFrame Fill(this IFrame parent, Gradient gradient) =>
        Attach(parent, new FillBlock { Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient)) });

    public static IFrame Stroke(this IFrame parent, float weight) =>
        Attach(parent, new StrokeBlock { Weight = Sides.All(weight) });

    public static IFrame StrokeLeft(this IFrame parent, float weight) =>
        Attach(parent, new StrokeBlock { Weight = Sides.Zero.WithLeft(weight) });

    public static IFrame StrokeRight(this IFrame parent, float weight) =>
        Attach(parent, new StrokeBlock { Weight = Sides.Zero.WithRight(weight) });

    public static IFrame StrokeTop(this IFrame parent, float weight) =>
        Attach(parent, new StrokeBlock { Weight = Sides.Zero.WithTop(weight) });

    public static IFrame StrokeBottom(this IFrame parent, float weight) =>
        Attach(parent, new StrokeBlock { Weight = Sides.Zero.WithBottom(weight) });

    /// <summary>
    /// Sets the ink of the stroke this directly follows.
    /// </summary>
    public static IFrame StrokeInk(this IFrame parent, Ink ink)
    {
        if (parent is not StrokeBlock stroke)
            throw new CompositionException("StrokeInk must directly follow Stroke, StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.");

        stroke.Ink = ink;
        return stroke;
    }

    public static IFrame StrokeInk(this IFrame parent, string hex) =>
        parent.StrokeInk(Ink.Hex(hex));

    /// <summary>
    /// Paints the stroke this directly follows in a gradient, laid across everything the stroke covers.
    /// </summary>
    public static IFrame StrokeInk(this IFrame parent, Gradient gradient)
    {
        ArgumentNullException.ThrowIfNull(gradient);

        if (parent is not StrokeBlock stroke)
            throw new CompositionException("StrokeInk must directly follow Stroke, StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.");

        stroke.Gradient = gradient;
        return stroke;
    }

    /// <summary>
    /// Casts a shadow from the frame onto what lies beneath it. Round its corners with <see cref="RoundCorners(IFrame, float)"/>
    /// directly after, to match a rounded fill.
    /// </summary>
    public static IFrame DropShadow(this IFrame parent, Shadow shadow)
    {
        if (!(shadow.Blur >= 0) || float.IsInfinity(shadow.Blur))
            throw new ArgumentOutOfRangeException(nameof(shadow), shadow.Blur, "A shadow's blur is a finite number of points, not negative.");

        if (!IsFinite(shadow.Spread) || !IsFinite(shadow.Offset.X) || !IsFinite(shadow.Offset.Y))
            throw new ArgumentOutOfRangeException(nameof(shadow), "A shadow's offset and spread are finite numbers of points.");

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        return Attach(parent, new ShadowBlock { Shadow = shadow });
    }

    /// <summary>Casts a shadow of <paramref name="ink"/>, blurred by <paramref name="blur"/> and moved by the offset.</summary>
    public static IFrame DropShadow(this IFrame parent, Ink ink, float blur, float offsetX = 0, float offsetY = 0, float spread = 0) =>
        parent.DropShadow(new Shadow(ink, blur, new Offset(offsetX, offsetY), spread));

    /// <summary>
    /// Rounds the corners of the fill, stroke or shadow this directly follows.
    /// </summary>
    public static IFrame RoundCorners(this IFrame parent, float radius) => parent.RoundCorners(Corners.All(radius));

    /// <summary>
    /// Rounds each corner of the fill or stroke this directly follows to its own radius, clockwise from the top left;
    /// zero leaves a corner square.
    /// </summary>
    public static IFrame RoundCorners(this IFrame parent, float topLeft, float topRight, float bottomRight, float bottomLeft) =>
        parent.RoundCorners(new Corners(topLeft, topRight, bottomRight, bottomLeft));

    /// <summary>Rounds each corner of the fill, stroke or shadow this directly follows to its radius in <paramref name="corners"/>.</summary>
    public static IFrame RoundCorners(this IFrame parent, Corners corners) => parent switch
    {
        FillBlock fill => Assign(fill, corners),
        StrokeBlock stroke => Assign(stroke, corners),
        ShadowBlock shadow => Assign(shadow, corners),
        _ => throw new CompositionException("RoundCorners must directly follow Fill, DropShadow or a Stroke method.")
    };

    /// <summary>
    /// Sets where the stroke this directly follows lies against the frame's edge: inside it, the default, centred
    /// on it, or outside it.
    /// </summary>
    public static IFrame AlignStroke(this IFrame parent, StrokeAlignment alignment)
    {
        if (parent is not StrokeBlock stroke)
            throw new CompositionException("AlignStroke must directly follow Stroke, StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.");

        stroke.Alignment = alignment;
        return stroke;
    }

    private static IFrame Assign(FillBlock fill, Corners corners)
    {
        fill.Corners = corners;
        return fill;
    }

    private static IFrame Assign(ShadowBlock shadow, Corners corners)
    {
        shadow.Corners = corners;
        return shadow;
    }

    private static IFrame Assign(StrokeBlock stroke, Corners corners)
    {
        // A rounded corner has no shape where two different weights meet, so the block ignores the radius unless
        // every side matches. Saying so here beats accepting the call and quietly drawing square corners.
        if (corners.IsRounded && !stroke.HasUniformWeight)
        {
            throw new CompositionException(
                "RoundCorners needs a stroke of one weight on every side, greater than zero. Use Stroke(weight) " +
                "rather than StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.");
        }

        stroke.Corners = corners;
        return stroke;
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

    public static IFrame Expand(this IFrame parent) =>
        Attach(parent, new ExpandBlock { Horizontally = true, Vertically = true });

    public static IFrame ExpandHorizontally(this IFrame parent) =>
        Attach(parent, new ExpandBlock { Horizontally = true });

    public static IFrame ExpandVertically(this IFrame parent) =>
        Attach(parent, new ExpandBlock { Vertically = true });

    public static IFrame Proportion(this IFrame parent, float ratio, ProportionFit fit = ProportionFit.Width) =>
        Attach(parent, new ProportionBlock { Ratio = ratio, Fit = fit });

    /// <summary>Shrinks the content just enough to fit the space available.</summary>
    public static IFrame ShrinkToFit(this IFrame parent, float minScale = 0.25f) =>
        Attach(parent, new ShrinkToFitBlock { MinScale = minScale });

    /// <summary>Mirrors the content left to right.</summary>
    public static IFrame MirrorHorizontal(this IFrame parent) =>
        Attach(parent, new MirrorBlock { Horizontally = true });

    /// <summary>Mirrors the content top to bottom.</summary>
    public static IFrame MirrorVertical(this IFrame parent) =>
        Attach(parent, new MirrorBlock { Vertically = true });

    /// <summary>Mirrors the content on both axes, equivalent to a half turn.</summary>
    public static IFrame MirrorBoth(this IFrame parent) =>
        Attach(parent, new MirrorBlock { Horizontally = true, Vertically = true });

    // ---- Alignment -----------------------------------------------------------------------------------------

    public static IFrame FlushLeft(this IFrame parent) => Place(parent, horizontal: HorizontalPlacement.Left);

    public static IFrame Centered(this IFrame parent) => Place(parent, horizontal: HorizontalPlacement.Center);

    public static IFrame FlushRight(this IFrame parent) => Place(parent, horizontal: HorizontalPlacement.Right);

    public static IFrame FlushTop(this IFrame parent) => Place(parent, vertical: VerticalPlacement.Top);

    public static IFrame Middle(this IFrame parent) => Place(parent, vertical: VerticalPlacement.Middle);

    public static IFrame FlushBottom(this IFrame parent) => Place(parent, vertical: VerticalPlacement.Bottom);

    /// <summary>
    /// Reuses an adjacent alignment element when one is already present, so that <c>.FlushRight().Middle()</c>
    /// aligns on both axes instead of nesting two elements that each claim the full space.
    /// </summary>
    private static IFrame Place(IFrame parent, HorizontalPlacement? horizontal = null, VerticalPlacement? vertical = null)
    {
        // Only fold into an aligner that is still empty. Once it has content, `.FlushRight()` on the same slot
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

    public static IFrame ShiftAcross(this IFrame parent, float value) =>
        Attach(parent, new ShiftBlock { Offset = new Offset(value, 0) });

    public static IFrame ShiftDown(this IFrame parent, float value) =>
        Attach(parent, new ShiftBlock { Offset = new Offset(0, value) });

    public static IFrame Scale(this IFrame parent, float factor) =>
        Attach(parent, new ScaleBlock { ScaleX = factor, ScaleY = factor });

    public static IFrame Scale(this IFrame parent, float scaleX, float scaleY) =>
        Attach(parent, new ScaleBlock { ScaleX = scaleX, ScaleY = scaleY });

    /// <summary>Rotates a quarter turn anticlockwise, swapping the layout axes.</summary>
    public static IFrame TurnLeft(this IFrame parent) =>
        Attach(parent, new TurnBlock { QuarterTurns = 3 });

    /// <summary>Rotates a quarter turn clockwise, swapping the layout axes.</summary>
    public static IFrame TurnRight(this IFrame parent) =>
        Attach(parent, new TurnBlock { QuarterTurns = 1 });

    /// <summary>
    /// Rotates by any angle in degrees, clockwise, about the centre of the frame. Layout is unaffected: the content
    /// keeps the room it was given and may reach past it. For quarter turns that swap the layout axes, use
    /// <see cref="TurnLeft"/> or <see cref="TurnRight"/>.
    /// </summary>
    public static IFrame Rotate(this IFrame parent, float degrees) =>
        Attach(parent, new RotateBlock { Degrees = degrees });

    // ---- Flow control --------------------------------------------------------------------------------------

    public static IFrame When(this IFrame parent, bool condition) =>
        Attach(parent, new WhenBlock { Condition = condition });

    /// <summary>
    /// Shows the content only on pages <paramref name="condition"/> accepts, such as odd pages or all but the first.
    /// </summary>
    /// <remarks>
    /// The page count is null until the engine has counted the pages. Content shown or hidden by it can change that
    /// count, so it suits content of a fixed size, such as a mark in a margin, rather than content in the flow.
    /// </remarks>
    public static IFrame When(this IFrame parent, Func<PageFacts, bool> condition) =>
        Attach(parent, new WhenBlock { OnPage = condition ?? throw new ArgumentNullException(nameof(condition)) });

    /// <summary>
    /// Draws the content again on every page its container continues onto: a row's column is drawn afresh beside
    /// the columns still going, instead of being left empty once its content is used up.
    /// </summary>
    public static IFrame RepeatOnEachPage(this IFrame parent) =>
        Attach(parent, new RepeatBlock());

    /// <summary>
    /// Draws as much of the content as fits where it first appears and discards the overset, the rest that does not
    /// fit, instead of continuing it on the next page. Content that fits nowhere takes no room.
    /// </summary>
    public static IFrame DiscardOverset(this IFrame parent) =>
        Attach(parent, new DiscardOversetBlock());

    public static IFrame Once(this IFrame parent) =>
        Attach(parent, new OnceBlock());

    public static IFrame SkipFirst(this IFrame parent) =>
        Attach(parent, new SkipFirstBlock());

    public static void NewPage(this IFrame parent) =>
        Attach(parent, new NewPageBlock());

    // ---- Inherited context ---------------------------------------------------------------------------------

    /// <summary>Sets the flow direction for everything nested inside.</summary>
    public static IFrame Reading(this IFrame parent, ReadingDirection direction) =>
        Attach(parent, new ReadingDirectionBlock { ReadingDirection = direction });

    /// <summary>Lays out nested content right to left.</summary>
    public static IFrame RightToLeft(this IFrame parent) =>
        parent.Reading(ReadingDirection.RightToLeft);

    /// <summary>Lays out nested content left to right.</summary>
    public static IFrame LeftToRight(this IFrame parent) =>
        parent.Reading(ReadingDirection.LeftToRight);

    /// <summary>Adjusts the style inherited by all text nested inside.</summary>
    public static IFrame DefaultType(this IFrame parent, Func<TypeStyle, TypeStyle> refinement)
    {
        ArgumentNullException.ThrowIfNull(refinement);
        return Attach(parent, new DefaultTypeBlock { Refinement = refinement });
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    /// <summary>
    /// Lets content exceed the space offered to it while reporting no size to its parent.
    /// </summary>
    public static IFrame Unbounded(this IFrame parent) =>
        Attach(parent, new UnboundedBlock());

    /// <summary>
    /// Prevents content from being split across pages, moving it whole to the next page instead.
    /// </summary>
    public static IFrame KeepTogether(this IFrame parent) =>
        Attach(parent, new KeepTogetherBlock());

    /// <summary>
    /// Defers the content to the next page unless at least <paramref name="minHeight"/> remains, so a heading
    /// or short block is never stranded at the bottom of a page.
    /// </summary>
    public static IFrame RequireSpace(this IFrame parent, float minHeight) =>
        Attach(parent, new RequireSpaceBlock { MinHeight = minHeight });

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    /// <summary>
    /// Draws a horizontal rule across the available width, solid unless <paramref name="style"/> says otherwise. A
    /// wavy rule takes three times its weight, the wave swinging a weight either side of its centre.
    /// </summary>
    public static void Rule(this IFrame parent, float weight = 1f, Ink? ink = null, StrokeStyle style = StrokeStyle.Solid) =>
        Attach(parent, new RuleBlock { Weight = weight, Ink = ink ?? Ink.Black, Style = style });

    /// <summary>
    /// Draws a horizontal rule across the available width in dashes and gaps of the lengths in
    /// <paramref name="dashes"/>, alternating and starting with a dash: <c>[4, 2]</c> is dashes of 4 points 2 apart.
    /// </summary>
    public static void Rule(this IFrame parent, float weight, Ink ink, IReadOnlyList<float> dashes) =>
        Attach(parent, new RuleBlock { Weight = weight, Ink = ink, Dashes = Checked(dashes) });

    /// <summary>Draws a horizontal rule across the available width in a gradient along its length.</summary>
    public static void Rule(this IFrame parent, float weight, Gradient gradient, StrokeStyle style = StrokeStyle.Solid) =>
        Attach(parent, new RuleBlock { Weight = weight, Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient)), Style = style });

    /// <summary>Draws a horizontal rule in a gradient, in dashes and gaps of the lengths in <paramref name="dashes"/>.</summary>
    public static void Rule(this IFrame parent, float weight, Gradient gradient, IReadOnlyList<float> dashes) =>
        Attach(parent, new RuleBlock { Weight = weight, Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient)), Dashes = Checked(dashes) });

    /// <summary>Draws a vertical rule down the available height, solid unless <paramref name="style"/> says otherwise.</summary>
    public static void VerticalRule(this IFrame parent, float weight = 1f, Ink? ink = null, StrokeStyle style = StrokeStyle.Solid) =>
        Attach(parent, new VerticalRuleBlock { Weight = weight, Ink = ink ?? Ink.Black, Style = style });

    /// <summary>Draws a vertical rule down the available height in dashes and gaps of the lengths in <paramref name="dashes"/>.</summary>
    public static void VerticalRule(this IFrame parent, float weight, Ink ink, IReadOnlyList<float> dashes) =>
        Attach(parent, new VerticalRuleBlock { Weight = weight, Ink = ink, Dashes = Checked(dashes) });

    /// <summary>Draws a vertical rule down the available height in a gradient along its length.</summary>
    public static void VerticalRule(this IFrame parent, float weight, Gradient gradient, StrokeStyle style = StrokeStyle.Solid) =>
        Attach(parent, new VerticalRuleBlock { Weight = weight, Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient)), Style = style });

    /// <summary>Draws a vertical rule in a gradient, in dashes and gaps of the lengths in <paramref name="dashes"/>.</summary>
    public static void VerticalRule(this IFrame parent, float weight, Gradient gradient, IReadOnlyList<float> dashes) =>
        Attach(parent, new VerticalRuleBlock { Weight = weight, Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient)), Dashes = Checked(dashes) });

    /// <summary>
    /// A copy of a dash pattern, checked now rather than when the page is drawn: a pattern of no lengths, a negative
    /// length, or only gaps of nothing would draw no dash at all.
    /// </summary>
    private static float[] Checked(IReadOnlyList<float> dashes)
    {
        ArgumentNullException.ThrowIfNull(dashes);

        if (dashes.Count == 0 || dashes.Any(length => !(length >= 0) || float.IsInfinity(length)) || dashes.All(length => length == 0))
            throw new ArgumentException("A dash pattern needs lengths that are finite, none negative and not all zero.", nameof(dashes));

        return dashes.ToArray();
    }

    /// <summary>Fills the available space with a block standing in for unwritten content.</summary>
    public static void Placeholder(this IFrame parent, Ink? ink = null) =>
        Attach(parent, new PlaceholderBlock { Ink = ink ?? Ink.Rgb(0xEE, 0xEE, 0xEE) });

    // ---- Links ---------------------------------------------------------------------------------------------

    public static IFrame Link(this IFrame parent, string url)
    {
        // An empty target makes the element draw no annotation at all, so the region would look linked in the
        // source and do nothing in the file.
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return Attach(parent, new LinkBlock { Url = url });
    }

    /// <summary>Marks this content as a named destination that <see cref="CrossReference"/> can target.</summary>
    public static IFrame Anchor(this IFrame parent, string name)
    {
        // An unnamed section registers no destination, so every link and page reference aimed at it would
        // silently resolve to nothing for the life of the document.
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Attach(parent, new AnchorBlock { Name = name });
    }

    public static IFrame CrossReference(this IFrame parent, string anchor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchor);

        return Attach(parent, new CrossReferenceBlock { Anchor = anchor });
    }
}
