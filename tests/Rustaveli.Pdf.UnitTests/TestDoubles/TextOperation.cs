namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record TextOperation(Offset Position, string Text, TypeStyle Style) : DrawOperation(Position);
