namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record ImageOperation(Offset Position, Extent Size, Bounds Bounds) : DrawOperation(Position);
