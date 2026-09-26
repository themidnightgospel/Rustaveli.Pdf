namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record InternalLinkOperation(Offset Position, Extent Size, string Destination, Bounds Bounds)
    : DrawOperation(Position);
