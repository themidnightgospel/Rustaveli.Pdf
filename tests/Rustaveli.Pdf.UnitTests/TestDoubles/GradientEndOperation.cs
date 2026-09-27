namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>The end of a gradient: the shapes recorded after it are painted in their own ink again.</summary>
internal sealed record GradientEndOperation() : DrawOperation(Offset.Zero);
