using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Draws artwork, a step at a time, as a pen on a page would: fills, strokes and text, under transforms and clips that
/// last until the state saved before them is restored.
/// </summary>
public sealed class ArtworkComposer
{
    private const string WritableNumbers = "Artwork is drawn with numbers a PDF can hold: finite, and below 10^15 in magnitude.";

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
    public void Translate(float x, float y)
    {
        RequireWritable(x, nameof(x));
        RequireWritable(y, nameof(y));
        _steps.Add((surface, _) => surface.Translate(new Offset(x, y)));
    }

    /// <summary>Scales what follows, across and down.</summary>
    public void Scale(float x, float y)
    {
        RequireWritable(x, nameof(x));
        RequireWritable(y, nameof(y));
        _steps.Add((surface, _) => surface.Scale(x, y));
    }

    /// <summary>Turns what follows by <paramref name="degrees"/>, clockwise about the origin.</summary>
    public void Rotate(float degrees)
    {
        RequireWritable(degrees, nameof(degrees));
        _steps.Add((surface, _) => surface.Rotate(degrees));
    }

    /// <summary>
    /// Transforms what follows by the matrix mapping (x, y) to (a·x + c·y + e, b·x + d·y + f), as SVG's
    /// <c>matrix(a b c d e f)</c> does.
    /// </summary>
    public void Transform(float a, float b, float c, float d, float e, float f)
    {
        RequireWritable(a, nameof(a));
        RequireWritable(b, nameof(b));
        RequireWritable(c, nameof(c));
        RequireWritable(d, nameof(d));
        RequireWritable(e, nameof(e));
        RequireWritable(f, nameof(f));
        _steps.Add((surface, _) => surface.Concatenate(a, b, c, d, e, f));
    }

    /// <summary>Confines what follows, until the next <see cref="RestoreState"/>, to the inside of <paramref name="path"/>.</summary>
    public void Clip(VectorPath path, FillRule rule = FillRule.NonZero)
    {
        VectorPath drawn = WritableCopy(path);
        _steps.Add((surface, _) => surface.ClipPath(drawn, rule));
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="ink"/>.</summary>
    public void Fill(VectorPath path, Ink ink, FillRule rule = FillRule.NonZero)
    {
        VectorPath drawn = WritableCopy(path);
        _steps.Add((surface, _) => surface.FillPath(drawn, ink, rule));
    }

    /// <summary>Fills <paramref name="path"/> with <paramref name="gradient"/>, laid across the path's bounds.</summary>
    public void Fill(VectorPath path, Gradient gradient, FillRule rule = FillRule.NonZero)
    {
        VectorPath drawn = WritableCopy(path);
        (Offset position, Extent size) = RequireWritable(gradient, drawn);
        _steps.Add((surface, _) =>
        {
            surface.BeginGradient(gradient, position, size);
            surface.FillPath(drawn, Ink.Black, rule);
            surface.EndGradient();
        });
    }

    /// <summary>Strokes <paramref name="path"/> with <paramref name="ink"/>, as <paramref name="style"/> says.</summary>
    public void Stroke(VectorPath path, Ink ink, LineStyle style)
    {
        VectorPath drawn = WritableCopy(path);
        LineStyle line = WritableCopy(style);
        _steps.Add((surface, _) => surface.StrokePath(drawn, ink, line));
    }

    /// <summary>Strokes <paramref name="path"/> with <paramref name="gradient"/>, laid across the path's bounds.</summary>
    public void Stroke(VectorPath path, Gradient gradient, LineStyle style)
    {
        VectorPath drawn = WritableCopy(path);
        LineStyle line = WritableCopy(style);
        (Offset position, Extent size) = RequireWritable(gradient, drawn);
        _steps.Add((surface, _) =>
        {
            surface.BeginGradient(gradient, position, size);
            surface.StrokePath(drawn, Ink.Black, line);
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
        RequireWritable(x, nameof(x));
        RequireWritable(y, nameof(y));
        _steps.Add((surface, measurer) =>
        {
            float width = measurer.MeasureWidth(text, style);
            float start = x - (anchor == TextAnchor.Start ? 0 : width * (anchor == TextAnchor.Middle ? 0.5f : 1f));

            // Text so large that its glyphs would be placed beyond the numbers a PDF can hold is left out.
            if (Writable.Is(start) && Writable.Is(start + width))
                surface.DrawText(text, new Offset(start, y), style);
        });
    }

    /// <summary>
    /// Sets runs of text one after another on a line, as SVG's text and its spans are set: a run placed at an x or a y
    /// of its own starts a new chunk there, anchored as its first run says; any other run goes on from where the one
    /// before it ended, as measured when the artwork is drawn, moved by its dx and dy.
    /// </summary>
    internal void TextRuns(IReadOnlyList<Span> runs)
    {
        Span[] set = runs.ToArray();

        _steps.Add((surface, measurer) =>
        {
            float penX = 0, penY = 0;

            for (int start = 0; start < set.Length;)
            {
                int end = start + 1;

                while (end < set.Length && set[end].X is null && set[end].Y is null)
                    end++;

                // Each run's place before anchoring, the pen moving on by the width of each.
                float[] xs = new float[end - start], ys = new float[end - start], widths = new float[end - start];
                penX = set[start].X ?? penX;
                penY = set[start].Y ?? penY;

                for (int index = start; index < end; index++)
                {
                    penX += set[index].Dx;
                    penY += set[index].Dy;
                    xs[index - start] = penX;
                    ys[index - start] = penY;
                    widths[index - start] = measurer.MeasureWidth(set[index].Text, set[index].Style);
                    penX += widths[index - start];
                }

                float extent = penX - xs[0];
                float shift = set[start].Anchor switch
                {
                    TextAnchor.Middle => extent / 2,
                    TextAnchor.End => extent,
                    _ => 0,
                };

                for (int index = start; index < end; index++)
                {
                    float x = xs[index - start] - shift;

                    // Text so large, or so far off, that its glyphs would be placed beyond the numbers a PDF can hold
                    // is left out; a run of spaces is only room.
                    if (set[index].Visible && set[index].Text.Trim().Length > 0 && Writable.Is(x) && Writable.Is(x + widths[index - start]) && Writable.Is(ys[index - start]))
                        surface.DrawText(set[index].Text, new Offset(x, ys[index - start]), set[index].Style);
                }

                start = end;
            }
        });
    }

    /// <summary>Places <paramref name="image"/> in the box at (<paramref name="x"/>, <paramref name="y"/>), stretched to fill it.</summary>
    public void Image(IImage image, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(image);
        RequireWritable(x, nameof(x));
        RequireWritable(y, nameof(y));
        RequireWritable(width, nameof(width));
        RequireWritable(height, nameof(height));
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

    /// <summary>
    /// Whether <paramref name="gradient"/> can be laid across <paramref name="path"/>: its axis, from the path's bounds,
    /// can be written to a PDF. Gradients read from outside are checked with this before they are drawn.
    /// </summary>
    internal static bool CanLay(Gradient gradient, VectorPath path)
    {
        (Offset position, Extent size) = path.Bounds();
        (Offset start, Offset end) = gradient.Axis(position, size);
        return Writable.Is(start) && Writable.Is(end);
    }

    /// <summary>
    /// One run of text for <see cref="TextRuns"/>: its style, where it is placed — an x or a y of its own starting a
    /// chunk, null going on from the run before — how far it is moved from there, and whether it is drawn or only takes
    /// its room.
    /// </summary>
    internal sealed class Span(string text, TypeStyle style, TextAnchor anchor, bool visible)
    {
        public string Text { get; set; } = text;

        public TypeStyle Style { get; } = style;

        public TextAnchor Anchor { get; } = anchor;

        public bool Visible { get; } = visible;

        public float? X { get; init; }

        public float? Y { get; init; }

        public float Dx { get; init; }

        public float Dy { get; init; }
    }

    private static void RequireWritable(float value, string name)
    {
        if (!Writable.Is(value))
            throw new ArgumentOutOfRangeException(name, value, WritableNumbers);
    }

    /// <summary>
    /// A copy of <paramref name="path"/> as it is at the call, checked. The artwork is drawn later, when it is placed,
    /// so drawing the caller's own path would draw whatever it had become by then, beyond the check made here.
    /// </summary>
    private static VectorPath WritableCopy(VectorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        VectorPath copy = path.Copy();

        if (!copy.IsWritable)
            throw new ArgumentOutOfRangeException(nameof(path), "Every point of a path is a number a PDF can hold: finite, and below 10^15 in magnitude.");

        return copy;
    }

    /// <summary>A copy of <paramref name="style"/> with its own dashes, which a later change to the caller's list does not reach, checked.</summary>
    private static LineStyle WritableCopy(LineStyle style)
    {
        LineStyle copy = style with { Dashes = style.Dashes?.ToArray() };

        if (!Writable.Is(copy.Weight) || !Writable.Is(copy.MiterLimit) || !Writable.Is(copy.DashOffset) || (copy.Dashes is { } dashes && !dashes.All(Writable.Is)))
            throw new ArgumentOutOfRangeException(nameof(style), "A line's weight, miter limit and dashes are numbers a PDF can hold: finite, and below 10^15 in magnitude.");

        return copy;
    }

    /// <summary>The bounds of <paramref name="path"/>, across which <paramref name="gradient"/> is laid.</summary>
    private static (Offset Position, Extent Size) RequireWritable(Gradient gradient, VectorPath path)
    {
        ArgumentNullException.ThrowIfNull(gradient);

        if (!CanLay(gradient, path))
            throw new ArgumentOutOfRangeException(nameof(gradient), "The gradient, laid across this path, reaches beyond the numbers a PDF can hold: finite, and below 10^15 in magnitude.");

        return path.Bounds();
    }
}
