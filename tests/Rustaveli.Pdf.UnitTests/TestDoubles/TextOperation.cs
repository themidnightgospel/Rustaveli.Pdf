namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record TextOperation(Position Position, string Text, TextStyle Style) : DrawOperation(Position);
