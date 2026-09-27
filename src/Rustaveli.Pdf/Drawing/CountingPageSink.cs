using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Discards every drawing operation.
/// </summary>
/// <remarks>
/// Used for the counting pass, where the engine needs to know how many pages a document produces before it can
/// resolve content such as "page 2 of 7". Running the identical layout against a sink that draws nothing keeps
/// the two passes consistent without emitting output twice.
/// </remarks>
internal sealed class CountingPageSink : IPageSink
{
    // Nothing is drawn, but where content lands is still recorded, so the transforms are followed.
    private readonly TransformTracker _transform = new TransformTracker();

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

    public void Translate(Offset offset) => _transform.Translate(offset);

    public void Scale(float scaleX, float scaleY) => _transform.Scale(scaleX, scaleY);

    public void Rotate(float degrees) => _transform.Rotate(degrees);

    public void Concatenate(float a, float b, float c, float d, float e, float f) => _transform.Concatenate(a, b, c, d, e, f);

    public void ClipRectangle(Extent size)
    {
    }

    public void DrawRectangle(Offset position, Extent size, Ink color)
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

    public void DrawText(string text, Offset baselineStart, TypeStyle style, bool rightToLeft = false)
    {
    }

    public void DrawImage(IImage image, Extent size)
    {
    }

    public void DrawExternalLink(string url, Extent size)
    {
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
    }

    public void DrawDestination(string destinationName)
    {
    }

    public void DrawBookmark(string title, int level)
    {
    }

    public void Tag(StructureElement? element)
    {
    }

    public void Dispose()
    {
    }
}
