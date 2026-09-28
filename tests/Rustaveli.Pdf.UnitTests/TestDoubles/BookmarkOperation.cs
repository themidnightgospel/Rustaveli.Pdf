namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record BookmarkOperation(Offset Position, string Title, int Level) : DrawOperation(Position);
