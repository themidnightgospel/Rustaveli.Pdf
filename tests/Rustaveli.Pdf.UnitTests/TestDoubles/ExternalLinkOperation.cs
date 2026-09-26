namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record ExternalLinkOperation(Offset Position, Extent Size, string Url, Bounds Bounds) : DrawOperation(Position);
