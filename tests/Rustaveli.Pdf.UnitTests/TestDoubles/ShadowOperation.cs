namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record ShadowOperation(Offset Position, Extent Size, Corners Corners, Shadow Shadow, Bounds Bounds) : DrawOperation(Position);
