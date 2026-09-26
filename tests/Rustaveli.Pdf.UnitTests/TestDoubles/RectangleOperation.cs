namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record RectangleOperation(Offset Position, Extent Size, Ink Ink, Bounds Bounds) : DrawOperation(Position);
