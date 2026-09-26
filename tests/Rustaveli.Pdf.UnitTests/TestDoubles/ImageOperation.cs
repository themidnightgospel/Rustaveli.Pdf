namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record ImageOperation(Position Position, Size Size, Bounds Bounds) : DrawOperation(Position);
