namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record ExternalLinkOperation(Position Position, Size Size, string Url, Bounds Bounds) : DrawOperation(Position);
