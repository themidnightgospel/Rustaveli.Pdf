namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A gradient set across a box, painting the shapes recorded after it until an <see cref="GradientEndOperation"/>.
/// </summary>
internal sealed record GradientOperation(Offset Position, Extent Size, Gradient Gradient, Bounds Bounds) : DrawOperation(Position);
