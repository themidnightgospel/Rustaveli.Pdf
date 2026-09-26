namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RectangleOperation(Offset Position, Extent Size, Ink Color, Bounds Bounds) : DrawOperation(Position);
