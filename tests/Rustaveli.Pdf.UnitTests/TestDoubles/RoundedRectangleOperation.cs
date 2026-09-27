namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record RoundedRectangleOperation(
    Offset Position, Extent Size, Corners Corners, Ink Ink, float StrokeWidth, Bounds Bounds)
    : DrawOperation(Position)
{
    /// <summary>The top-left radius, which is every corner's where the corners are alike.</summary>
    public float Radius => Corners.TopLeft;
}
