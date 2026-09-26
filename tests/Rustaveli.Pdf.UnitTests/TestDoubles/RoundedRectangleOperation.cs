namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RoundedRectangleOperation(
    Offset Position, Extent Size, float CornerRadius, Ink Color, float StrokeWidth, Bounds Bounds)
    : DrawOperation(Position);
