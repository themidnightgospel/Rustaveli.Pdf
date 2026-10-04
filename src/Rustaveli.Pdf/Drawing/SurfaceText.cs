namespace Rustaveli.Pdf.Drawing;

/// <summary>Setting a whole string, for callers that have one rather than a slice of one.</summary>
internal static class SurfaceText
{
    /// <summary>Sets <paramref name="text"/> as <see cref="ISurface.ShowText"/> sets a slice; null sets nothing.</summary>
    public static void ShowText(this ISurface surface, string? text, Offset baseline, TypeStyle style, ReadingDirection direction) =>
        surface.ShowText(text.AsMemory(), baseline, style, direction);
}
