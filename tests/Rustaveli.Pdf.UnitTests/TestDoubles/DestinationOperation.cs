namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record DestinationOperation(Offset Position, string Name) : DrawOperation(Position);
