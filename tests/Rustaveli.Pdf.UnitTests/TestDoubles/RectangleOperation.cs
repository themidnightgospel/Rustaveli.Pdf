namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RectangleOperation(Offset Position, Extent Size, Ink Ink, Bounds Bounds) : DrawOperation(Position);
