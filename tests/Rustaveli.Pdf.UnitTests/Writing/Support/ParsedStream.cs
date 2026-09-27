namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>A stream object read back from PDF syntax: its dictionary and its still-encoded data.</summary>
internal sealed record ParsedStream(Dictionary<string, object?> Dictionary, byte[] Data);
