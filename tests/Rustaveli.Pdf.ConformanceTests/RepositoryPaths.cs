using System.Runtime.CompilerServices;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>Locations in the source tree, found from this file's own path at compile time.</summary>
internal static class RepositoryPaths
{
    public static string Project { get; } = Path.GetDirectoryName(ThisFile())!;

    public static string Root { get; } = Path.GetFullPath(Path.Combine(Project, "..", ".."));

    /// <summary>Approved snapshots, committed alongside the tests.</summary>
    public static string ApprovedSnapshots { get; } = Path.Combine(Project, "Snapshots");

    /// <summary>Received and diff images from a failed comparison. Ignored by git; uploaded by CI.</summary>
    public static string ReceivedSnapshots { get; } = Path.Combine(Root, "artifacts", "snapshots");

    public static string Tools { get; } = Path.Combine(Root, "artifacts", "tools");

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
