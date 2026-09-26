namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record DestinationOperation(Position Position, string Name) : DrawOperation(Position);
