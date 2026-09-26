namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record InternalLinkOperation(Offset Position, Extent Size, string Destination, Bounds Bounds)
    : DrawOperation(Position);
