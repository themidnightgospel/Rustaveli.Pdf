namespace Rustaveli.Pdf.Testing;

/// <summary>The tests that change the working directory, run apart from every other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkingDirectoryCollection
{
    public const string Name = "Working directory";
}
