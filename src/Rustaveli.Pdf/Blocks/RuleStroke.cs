using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// How a rule is stroked: its weight, its ink or a gradient in its place, and either a <see cref="StrokeStyle"/> or a
/// pattern of dashes.
/// </summary>
internal readonly record struct RuleStroke(float Weight, Ink Ink, StrokeStyle Style, IReadOnlyList<float>? Dashes, Gradient? Gradient = null)
{
    /// <summary>
    /// How much room the rule takes across its length: its weight, or three times it for a wave, which swings a
    /// weight either side of its centre.
    /// </summary>
    public float Breadth => Dashes is null && Style == StrokeStyle.Wavy ? Weight * 3 : Weight;

    /// <summary>
    /// Draws the rule in the box of <paramref name="size"/> at the current origin: a solid rule as a filled bar, which
    /// is exact at any weight, anything else stroked along the centre line, across or down as <paramref name="across"/>
    /// says. A gradient is laid across the whole box.
    /// </summary>
    public void Draw(ISurface surface, Extent size, bool across)
    {
        if (Gradient is null)
        {
            Draw(surface, size, across, Ink);
            return;
        }

        surface.BeginGradient(Gradient, Offset.Zero, size);
        Draw(surface, size, across, Ink.Black);
        surface.EndGradient();
    }

    private void Draw(ISurface surface, Extent size, bool across, Ink ink)
    {
        if (Dashes is null && Style == StrokeStyle.Solid)
        {
            surface.FillRectangle(Offset.Zero, across ? new Extent(size.Width, Weight) : new Extent(Weight, size.Height), ink);
            return;
        }

        float middle = Breadth / 2;
        Offset from = across ? new Offset(0, middle) : new Offset(middle, 0);
        Offset to = across ? new Offset(size.Width, middle) : new Offset(middle, size.Height);

        if (Dashes is not null)
            surface.DrawDashedLine(from, to, Weight, ink, Dashes);
        else
            surface.DrawLine(from, to, Weight, ink, Style);
    }
}
