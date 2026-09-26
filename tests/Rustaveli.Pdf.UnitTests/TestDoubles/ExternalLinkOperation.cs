namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record ExternalLinkOperation(Offset Position, Extent Size, string Url, Bounds Bounds) : DrawOperation(Position);
