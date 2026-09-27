namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>A name read back from PDF syntax, with its escapes decoded.</summary>
internal sealed record ParsedName(string Value)
{
    public override string ToString() => "/" + Value;
}
