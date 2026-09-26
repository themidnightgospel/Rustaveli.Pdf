namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record DestinationOperation(Offset Position, string Name) : DrawOperation(Position);
