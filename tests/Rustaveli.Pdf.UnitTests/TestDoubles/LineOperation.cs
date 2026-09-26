namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record LineOperation(Position Position, Position End, float Thickness, Ink Color) : DrawOperation(Position);
