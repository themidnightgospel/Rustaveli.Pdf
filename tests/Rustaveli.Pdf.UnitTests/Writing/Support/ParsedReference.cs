namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>An indirect reference read back from PDF syntax.</summary>
internal sealed record ParsedReference(int ObjectNumber, int Generation);
