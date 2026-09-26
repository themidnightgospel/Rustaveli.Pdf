namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record InternalLinkOperation(Position Position, Size Size, string Destination, Bounds Bounds)
    : DrawOperation(Position);
