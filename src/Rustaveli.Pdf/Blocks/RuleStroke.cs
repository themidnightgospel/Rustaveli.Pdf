using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// How a rule is stroked: its weight and ink, and either a <see cref="StrokeStyle"/> or a pattern of dashes.
/// </summary>
internal readonly record struct RuleStroke(float Weight, Ink Ink, StrokeStyle Style, IReadOnlyList<float>? Dashes)
{
    /// <summary>
    /// How much room the rule takes across its length: its weight, or three times it for a wave, which swings a
    /// weight either side of its centre.
    /// </summary>
    public float Breadth => Dashes is null && Style == StrokeStyle.Wavy ? Weight * 3 : Weight;

    /// <summary>Strokes the rule along the centre line from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public void Draw(ISurface surface, Offset from, Offset to)
    {
        if (Dashes is not null)
            surface.DrawDashedLine(from, to, Weight, Ink, Dashes);
        else
            surface.DrawLine(from, to, Weight, Ink, Style);
    }
}
