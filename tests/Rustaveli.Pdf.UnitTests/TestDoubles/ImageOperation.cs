namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record ImageOperation(Offset Position, Extent Size, Bounds Bounds) : DrawOperation(Position);
