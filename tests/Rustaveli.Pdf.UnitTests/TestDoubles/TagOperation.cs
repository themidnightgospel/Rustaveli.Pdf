using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>What follows is drawn for <paramref name="Element"/>, or outside the structure for null.</summary>
internal sealed record TagOperation(Offset Position, StructureElement? Element) : DrawOperation(Position);
