namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record LineOperation(Offset Position, Offset End, float Thickness, Ink Ink) : DrawOperation(Position);
