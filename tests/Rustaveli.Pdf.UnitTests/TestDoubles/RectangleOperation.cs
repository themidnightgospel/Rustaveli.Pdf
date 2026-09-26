namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RectangleOperation(Position Position, Size Size, Ink Color, Bounds Bounds) : DrawOperation(Position);
