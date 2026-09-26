using Rustaveli.Pdf.Documents;

namespace Rustaveli.Pdf.ConformanceTests.Specimens;

/// <summary>A named document in the conformance corpus.</summary>
public sealed record Specimen(string Name, Func<Document> Build)
{
    // xUnit shows this in test names.
    public override string ToString() => Name;
}
