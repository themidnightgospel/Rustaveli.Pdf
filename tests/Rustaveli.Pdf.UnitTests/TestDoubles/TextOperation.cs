namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record TextOperation(Offset Position, string Text, TypeStyle Style) : DrawOperation(Position);
