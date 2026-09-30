using System.Globalization;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Holds the room its child is given below a maximum, and the room the child takes above a minimum, on either axis.
/// </summary>
/// <remarks>
/// A minimum larger than the maximum is not refused: the minimum wins what the child reports, the maximum what it is
/// offered.
/// </remarks>
internal sealed class ConstraintBlock : EnclosingBlock
{
    public float? MinWidth { get; set; }

    public float? MaxWidth { get; set; }

    public float? MinHeight { get; set; }

    public float? MaxHeight { get; set; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Exceeds(MinWidth, availableSpace.Width))
            return Fit.Defer(Shortfall("width", MinWidth!.Value, availableSpace.Width));

        if (Exceeds(MinHeight, availableSpace.Height))
            return Fit.Defer(Shortfall("height", MinHeight!.Value, availableSpace.Height));

        Fit plan = base.PlanCore(Capped(availableSpace), context);

        // Resizing leaves Nothing as it is: content used up leaves no box for a minimum to hold open.
        Extent size = new Extent(
            Raised(plan.Size.Width, MinWidth, availableSpace.Width),
            Raised(plan.Size.Height, MinHeight, availableSpace.Height));

        return Resized(plan, size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        Child?.Render(Capped(availableSpace), context);

    /// <summary>The room offered, no larger than the maximums on the axes that have one.</summary>
    private Extent Capped(Extent room) => new Extent(
        MaxWidth is { } width ? Math.Min(room.Width, width) : room.Width,
        MaxHeight is { } height ? Math.Min(room.Height, height) : room.Height);

    private static bool Exceeds(float? minimum, float available) =>
        minimum is { } length && length > available + Extent.Epsilon;

    /// <summary>A length raised to its minimum, where there is one, but never past the room there is.</summary>
    private static float Raised(float length, float? minimum, float available) =>
        Math.Min(minimum is { } floor ? Math.Max(length, floor) : length, available);

    private static string Shortfall(string axis, float minimum, float available) => string.Format(
        CultureInfo.InvariantCulture,
        "The content asks for a minimum {0} of {1:0.0} pt, but only {2:0.0} pt is available.",
        axis,
        minimum,
        available);
}
