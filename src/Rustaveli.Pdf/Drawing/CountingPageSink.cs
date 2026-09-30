using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A page sink that draws nothing and only counts the pages begun on it: the surface of the passes that work out how
/// many pages a document takes, and of drawing ahead to measure.
/// </summary>
/// <remarks>
/// <para>
/// Those passes run the very layout the final pass runs, so every drawing member accepts whatever it is given and
/// discards it without a word. The transform is followed all the same, because blocks read <see cref="Origin"/> to
/// record where their content was placed, and those records must match the final pass.
/// </para>
/// <para>
/// Blocks recognise this type to skip work nobody will see, such as shaping lines of plain text or generating
/// pictures, so it stays a class of its own rather than an option on another sink.
/// </para>
/// </remarks>
internal sealed class CountingPageSink : IPageSink
{
    private readonly TransformTracker _transform = new TransformTracker();

    /// <summary>How many pages have been begun.</summary>
    public int PageCount { get; private set; }

    public Offset Origin => _transform.Origin;

    public void BeginPage(Extent size)
    {
        PageCount++;
        _transform.Reset();
    }

    public void EndPage()
    {
    }

    public void Save() => _transform.Save();

    public void Restore() => _transform.Restore();

    public void MoveOrigin(Offset distance) => _transform.Translate(distance);

    public void ScaleAxes(float horizontal, float vertical) => _transform.Scale(horizontal, vertical);

    public void RotateClockwise(float degrees) => _transform.Rotate(degrees);

    public void Concatenate(float a, float b, float c, float d, float e, float f) =>
        _transform.Concatenate(a, b, c, d, e, f);

    public void ClipRectangle(Extent size)
    {
    }

    public void FillRectangle(Offset topLeft, Extent size, Ink ink)
    {
    }

    public void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f)
    {
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
    }

    public void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern)
    {
    }

    public void BeginGradient(Gradient gradient, Offset position, Extent size)
    {
    }

    public void EndGradient()
    {
    }

    public void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow)
    {
    }

    public void FillPath(VectorPath path, Ink ink, FillRule rule)
    {
    }

    public void StrokePath(VectorPath path, Ink ink, LineStyle style)
    {
    }

    public void ClipPath(VectorPath path, FillRule rule)
    {
    }

    public void ShowText(string text, Offset baseline, TypeStyle style, ReadingDirection direction)
    {
    }

    public void PaintImage(IImage image, Extent size)
    {
    }

    public void LinkToUrl(string url, Offset topLeft, Extent size)
    {
    }

    public void LinkToDestination(string destination, Offset topLeft, Extent size)
    {
    }

    public void NameDestination(string name, Offset at)
    {
    }

    public void DrawBookmark(string title, int level)
    {
    }

    public void Tag(StructureElement? element)
    {
    }

    /// <summary>There is nothing to let go of.</summary>
    public void Dispose()
    {
    }
}
