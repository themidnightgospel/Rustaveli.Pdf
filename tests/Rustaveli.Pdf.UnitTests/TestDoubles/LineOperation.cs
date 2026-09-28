namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record LineOperation(Offset Position, Offset End, float Thickness, Ink Ink, StrokeStyle Style = StrokeStyle.Solid, IReadOnlyList<float>? Dashes = null)
    : DrawOperation(Position);
