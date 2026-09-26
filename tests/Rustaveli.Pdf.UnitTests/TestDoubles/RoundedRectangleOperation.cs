namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RoundedRectangleOperation(
    Offset Position, Extent Size, float Radius, Ink Ink, float StrokeWidth, Bounds Bounds)
    : DrawOperation(Position);
