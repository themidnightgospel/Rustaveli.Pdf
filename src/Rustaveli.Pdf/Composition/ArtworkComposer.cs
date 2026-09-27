using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Draws artwork, a step at a time, as a pen on a page would: fills, strokes and text, under transforms and clips that
/// last until the state saved before them is restored.
/// </summary>
public sealed class ArtworkComposer
{
    private readonly List<Action<ISurface, ITypeMeasurer>> _steps = [];
    private int _saved;

    internal ArtworkComposer()
    {
    }

    /// <summary>Saves the transforms and clips in force, to be restored by <see cref="RestoreState"/>.</summary>
    public void SaveState()
    {
        _saved++;
        _steps.Add(static (surface, _) => surface.Save());
    }

    /// <summary>Returns to the transforms and clips in force at the matching <see cref="SaveState"/>.</summary>
    public void RestoreState()
    {
        if (_saved == 0)
            throw new InvalidOperationException("RestoreState has no SaveState to return to.");

        _saved--;
        _steps.Add(static (surface, _) => surface.Restore());
    }

    /// <summary>Moves the origin by (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void Translate(float x, float y) => _steps.Add((surface, _) => surface.Translate(new Offset(x, y)));

    /// <summary>Scales what follows, across and down.</summary>
    public void Scale(float x, float y) => _steps.Add((surface, _) => surface.Scale(x, y));

    /// <summary>Turns what follows by <paramref name="degrees"/>, clockwise about the origin.</summary>
    public void Rotate(float degrees) => _steps.Add((surface, _) => surface.Rotate(degrees));

    /// <summary>
    /// Transforms what follows by the matrix mapping (x, y) to (a·x + c·y + e, b·x + d·y + f), as SVG's
    /// <c>matrix(a b c d e f)</c> does.
    /// </summary>
    public void Transform(float a, float b, float c, float d, float e, float f) =>
        _steps.Add((surface, _) => surface.Concatenate(a, b, c, d, e, f));

    /// <summary>Confines what follows, until the next <see cref="RestoreState"/>, to the inside of <paramref name="path"/>.</summary>
    public void Clip(VectorPath path, FillRule rule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(path);
        _steps.Add((surface, _) => surface.ClipPath(path, rule));
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="ink"/>.</summary>
    public void Fill(VectorPath path, Ink ink, FillRule rule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(path);
        _steps.Add((surface, _) => surface.FillPath(path, ink, rule));
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="gradient"/>, laid across the path's bounds.</summary>
    public void Fill(VectorPath path, Gradient gradient, FillRule rule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(gradient);

        (Offset position, Extent size) = path.Bounds();
        _steps.Add((surface, _) =>
        {
            surface.BeginGradient(gradient, position, size);
            surface.FillPath(path, Ink.Black, rule);
            surface.EndGradient();
        });
    }

    /// <summary>Strokes <paramref name="path"/> with <paramref name="ink"/>, as <paramref name="style"/> says.</summary>
    public void Stroke(VectorPath path, Ink ink, LineStyle style)
    {
        ArgumentNullException.ThrowIfNull(path);
        _steps.Add((surface, _) => surface.StrokePath(path, ink, style));
    }

    /// <summary>Strokes <paramref name="path"/> with <paramref name="gradient"/>, laid across the path's bounds.</summary>
    public void Stroke(VectorPath path, Gradient gradient, LineStyle style)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(gradient);

        (Offset position, Extent size) = path.Bounds();
        _steps.Add((surface, _) =>
        {
            surface.BeginGradient(gradient, position, size);
            surface.StrokePath(path, Ink.Black, style);
            surface.EndGradient();
        });
    }

    /// <summary>
    /// Sets <paramref name="text"/> in one line on the baseline at <paramref name="y"/>, starting, centred on or ending
    /// at <paramref name="x"/> as <paramref name="anchor"/> says.
    /// </summary>
    public void Text(string text, float x, float y, TypeStyle style, TextAnchor anchor = TextAnchor.Start)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        _steps.Add((surface, measurer) =>
        {
            float shift = anchor == TextAnchor.Start ? 0 : measurer.MeasureWidth(text, style) * (anchor == TextAnchor.Middle ? 0.5f : 1f);
            surface.DrawText(text, new Offset(x - shift, y), style);
        });
    }

    /// <summary>Places <paramref name="image"/> in the box at (<paramref name="x"/>, <paramref name="y"/>), stretched to fill it.</summary>
    public void Image(IImage image, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(image);
        _steps.Add((surface, _) =>
        {
            surface.Save();
            surface.Translate(new Offset(x, y));
            surface.DrawImage(image, new Extent(width, height));
            surface.Restore();
        });
    }

    /// <summary>The steps drawn, with any state still saved restored at the end.</summary>
    internal IReadOnlyList<Action<ISurface, ITypeMeasurer>> Finish()
    {
        for (; _saved > 0; _saved--)
            _steps.Add(static (surface, _) => surface.Restore());

        return _steps.ToArray();
    }
}
