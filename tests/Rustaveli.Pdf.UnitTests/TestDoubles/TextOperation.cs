namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record TextOperation(Offset Position, string Text, TypeStyle Style, bool RightToLeft = false) : DrawOperation(Position);
