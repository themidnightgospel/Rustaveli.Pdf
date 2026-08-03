using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Shrinks its child just enough to fit the space available.
/// </summary>
/// <remarks>
/// The scale is found by bisection rather than arithmetic, because content does not scale linearly: narrowing a
/// paragraph re-wraps it, which changes its height by a step rather than a proportion. Each probe measures the
/// child at the candidate scale and asks whether the result fits.
/// </remarks>
public sealed class ScaleToFitElement : ContainerElement
{
    /// <summary>
    /// The smallest scale that will be tried before the content is passed through unscaled.
    /// </summary>
    /// <remarks>
    /// Zero or less means no lower bound. One or more disables shrinking altogether, making this element a
    /// pass-through rather than an unsatisfiable constraint.
    /// </remarks>
    public float MinScale { get; set; } = 0.25f;

    /// <summary>The floor actually searched from, with "no lower bound" resolved to a usable smallest step.</summary>
    private float EffectiveMinScale => MinScale <= 0f || float.IsNaN(MinScale) ? SmallestScale : MinScale;

    /// <summary>Below this the content is illegible anyway, so it stands in for "no lower bound".</summary>
    private const float SmallestScale = 0.01f;

    // Eight probes resolve the scale to under half a percent. Content height is a step function of width, so
    // finer probing buys nothing visible and every probe costs a full measurement of the subtree.
    private const int ProbeCount = 8;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (Child is null)
            return SpacePlan.FullRender(Size.Zero);

        float? scale = ResolveScale(availableSpace, context);

        // Content that can only ever render in instalments cannot be made to fit at any scale. Refusing it would
        // abort the whole document, so it is passed through instead and paginates as it would have unscaled.
        if (scale is null)
            return base.Measure(availableSpace, context);

        SpacePlan plan = Child.Measure(Unscale(availableSpace, scale.Value), context);

        if (plan.IsWrap || plan.IsEmpty)
            return plan;

        Size size = new Size(plan.Size.Width * scale.Value, plan.Size.Height * scale.Value);

        return SpacePlan.FullRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        float? scale = ResolveScale(availableSpace, context.Layout);

        // Matches Measure: content that could not be made to fit is drawn unscaled and left to paginate.
        if (scale is null)
        {
            base.Draw(availableSpace, context);
            return;
        }

        context.Canvas.Save();
        context.Canvas.Scale(scale.Value, scale.Value);

        Child.Draw(Unscale(availableSpace, scale.Value), context);

        context.Canvas.Restore();
    }

    private static Size Unscale(Size availableSpace, float scale) =>
        new(availableSpace.Width / scale, availableSpace.Height / scale);

    /// <summary>
    /// Finds the largest scale at or below 1 whose content fits, or null if even <see cref="MinScale"/> fails.
    /// </summary>
    private float? ResolveScale(Size availableSpace, LayoutContext context)
    {
        // A full-size render needs no search, and it is also the answer whenever shrinking is disallowed.
        if (Fits(availableSpace, 1f, context))
            return 1f;

        float floor = EffectiveMinScale;

        // "Never shrink" is a no-op rather than an unsatisfiable constraint.
        if (floor >= 1f)
            return null;

        if (!Fits(availableSpace, floor, context))
            return null;

        float low = floor;
        float high = 1f;

        for (int probe = 0; probe < ProbeCount; probe++)
        {
            float middle = (low + high) / 2;

            if (Fits(availableSpace, middle, context))
                low = middle;
            else
                high = middle;
        }

        return low;
    }

    private bool Fits(Size availableSpace, float scale, LayoutContext context)
    {
        if (scale <= 0)
            return false;

        SpacePlan plan = Child!.Measure(Unscale(availableSpace, scale), context);

        // Only a complete render counts: content that wrapped or split has not been made to fit.
        return plan.IsFullRender || plan.IsEmpty;
    }
}
