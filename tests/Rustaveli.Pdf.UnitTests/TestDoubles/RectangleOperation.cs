namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RectangleOperation(Position Position, Size Size, Color Color, Bounds Bounds) : DrawOperation(Position);
