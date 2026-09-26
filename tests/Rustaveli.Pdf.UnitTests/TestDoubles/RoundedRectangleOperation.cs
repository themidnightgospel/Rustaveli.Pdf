namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RoundedRectangleOperation(
    Position Position, Size Size, float CornerRadius, Ink Color, float StrokeWidth, Bounds Bounds)
    : DrawOperation(Position);
