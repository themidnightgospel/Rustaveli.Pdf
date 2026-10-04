using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A surface that draws nothing and keeps nothing, for measuring what drawing itself costs: unlike a recording surface,
/// it adds no allocations of its own.
/// </summary>
internal sealed class NullSurface : ISurface
{
    public Offset Origin { get; private set; }

    public void Save()
    {
    }

    public void Restore()
    {
    }

    public void MoveOrigin(Offset distance) => Origin = new Offset(Origin.X + distance.X, Origin.Y + distance.Y);

    public void ScaleAxes(float horizontal, float vertical)
    {
    }

    public void RotateClockwise(float degrees)
    {
    }

    public void Concatenate(float a, float b, float c, float d, float e, float f)
    {
    }

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

    public void ShowText(ReadOnlyMemory<char> text, Offset baseline, TypeStyle style, ReadingDirection direction)
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
}
