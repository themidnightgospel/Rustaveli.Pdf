namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Draws a page in order of draw order rather than in the order it was drawn: everything is held back until the page
/// ends, then drawn onto the pages beneath, the lowest order first.
/// </summary>
/// <remarks>
/// Each drawing is held with the transforms and clips in force when it was made, as a chain shared with every other
/// drawing made under them, and drawn again under that chain. Drawings in one order keep the order they were made
/// in, and consecutive ones under the same chain share one saved state.
/// </remarks>
internal sealed class LayeredPageSink(IPageSink pages) : IPageSink
{
    private readonly SortedDictionary<int, List<Held>> _orders = [];
    private readonly Stack<Change?> _saved = new Stack<Change?>();
    private readonly TransformTracker _transform = new TransformTracker();
    private Change? _state;

    /// <summary>The draw order drawings are held under, zero unless content says otherwise.</summary>
    public int Order { get; set; }

    public Offset Origin => _transform.Origin;

    public void BeginPage(Extent size)
    {
        pages.BeginPage(size);
        _orders.Clear();
        _saved.Clear();
        _transform.Reset();
        _state = null;
        Order = 0;
    }

    public void EndPage()
    {
        foreach (List<Held> drawings in _orders.Values)
        {
            Change? applied = null;
            bool open = false;

            foreach (Held held in drawings)
            {
                if (!open || !ReferenceEquals(held.State, applied))
                {
                    if (open)
                        pages.Restore();

                    pages.Save();
                    held.State?.Apply(pages);
                    applied = held.State;
                    open = true;
                }

                held.Draw(pages);
            }

            if (open)
                pages.Restore();
        }

        _orders.Clear();
        pages.EndPage();
    }

    public void Save()
    {
        _saved.Push(_state);
        _transform.Save();
    }

    public void Restore()
    {
        _state = _saved.Pop();
        _transform.Restore();
    }

    public void Translate(Offset offset)
    {
        _state = new Change(_state, surface => surface.Translate(offset));
        _transform.Translate(offset);
    }

    public void Scale(float scaleX, float scaleY)
    {
        _state = new Change(_state, surface => surface.Scale(scaleX, scaleY));
        _transform.Scale(scaleX, scaleY);
    }

    public void Rotate(float degrees)
    {
        _state = new Change(_state, surface => surface.Rotate(degrees));
        _transform.Rotate(degrees);
    }

    public void ClipRectangle(Extent size) => _state = new Change(_state, surface => surface.ClipRectangle(size));

    public void Concatenate(float a, float b, float c, float d, float e, float f)
    {
        _state = new Change(_state, surface => surface.Concatenate(a, b, c, d, e, f));
        _transform.Concatenate(a, b, c, d, e, f);
    }

    public void ClipPath(VectorPath path, FillRule rule) => _state = new Change(_state, surface => surface.ClipPath(path, rule));

    public void FillPath(VectorPath path, Ink ink, FillRule rule) => Hold(surface => surface.FillPath(path, ink, rule));

    public void StrokePath(VectorPath path, Ink ink, LineStyle style) => Hold(surface => surface.StrokePath(path, ink, style));

    public void DrawRectangle(Offset position, Extent size, Ink color) =>
        Hold(surface => surface.DrawRectangle(position, size, color));

    public void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f) =>
        Hold(surface => surface.DrawRoundedRectangle(position, size, corners, color, strokeWidth));

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid) =>
        Hold(surface => surface.DrawLine(from, to, thickness, color, style));

    public void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern) =>
        Hold(surface => surface.DrawDashedLine(from, to, thickness, color, pattern));

    public void BeginGradient(Gradient gradient, Offset position, Extent size) =>
        Hold(surface => surface.BeginGradient(gradient, position, size));

    public void EndGradient() => Hold(surface => surface.EndGradient());

    public void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow) =>
        Hold(surface => surface.DrawShadow(position, size, corners, shadow));

    public void DrawText(string text, Offset baselineStart, TypeStyle style, bool rightToLeft = false) =>
        Hold(surface => surface.DrawText(text, baselineStart, style, rightToLeft));

    public void DrawImage(IImage image, Extent size) => Hold(surface => surface.DrawImage(image, size));

    public void DrawExternalLink(string url, Extent size) => Hold(surface => surface.DrawExternalLink(url, size));

    public void DrawInternalLink(string destinationName, Extent size) =>
        Hold(surface => surface.DrawInternalLink(destinationName, size));

    public void DrawDestination(string destinationName) => Hold(surface => surface.DrawDestination(destinationName));

    public void DrawBookmark(string title, int level) => Hold(surface => surface.DrawBookmark(title, level));

    /// <summary>The pages beneath are the caller's, and closed by the caller.</summary>
    public void Dispose()
    {
    }

    private void Hold(Action<ISurface> draw)
    {
        if (!_orders.TryGetValue(Order, out List<Held>? drawings))
        {
            drawings = [];
            _orders.Add(Order, drawings);
        }

        drawings.Add(new Held(_state, draw));
    }

    /// <summary>A drawing, with the transforms and clips it was made under.</summary>
    private readonly record struct Held(Change? State, Action<ISurface> Draw);

    /// <summary>One transform or clip, after those before it.</summary>
    private sealed class Change(Change? before, Action<ISurface> change)
    {
        public void Apply(ISurface surface)
        {
            before?.Apply(surface);
            change(surface);
        }
    }
}
