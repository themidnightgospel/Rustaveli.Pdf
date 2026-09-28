namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed record PathOperation(
    Offset Position, PathPainting Painting, VectorPath Path, Ink Ink, FillRule Rule, LineStyle? Style, Bounds Bounds)
    : DrawOperation(Position);
