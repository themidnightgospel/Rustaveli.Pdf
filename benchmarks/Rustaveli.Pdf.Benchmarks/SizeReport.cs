using System.Globalization;
using System.Text.Json;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// File size per benchmark document for both libraries. Size is deterministic, so it needs one generation, not a
/// statistical run.
/// </summary>
public static class SizeReport
{
    /// <summary>Prints the sizes as a table and, given a path, writes them there as JSON too.</summary>
    /// <param name="json">Where to write the sizes as JSON, by document: the reference's and this library's, in bytes.</param>
    public static void Print(string? json)
    {
        Console.WriteLine("| Document | QuestPDF (bytes) | Rustaveli (bytes) | Ratio |");
        Console.WriteLine("|---|---:|---:|---:|");

        Dictionary<string, Sizes> sizes = new Dictionary<string, Sizes>(StringComparer.Ordinal);

        foreach (DocumentKind kind in Enum.GetValues<DocumentKind>())
        {
            int theirs = QuestDocuments.Generate(kind).Length;
            int ours = RustaveliDocuments.Generate(kind).Length;
            string ratio = ((double)ours / theirs).ToString("F2", CultureInfo.InvariantCulture);
            Console.WriteLine($"| {kind} | {theirs:N0} | {ours:N0} | {ratio}× |");
            sizes[kind.ToString()] = new Sizes(theirs, ours);
        }

        if (json is not null)
            File.WriteAllText(json, JsonSerializer.Serialize(sizes));
    }

    /// <summary>A document's size from each library.</summary>
    /// <param name="Reference">The reference library's, in bytes.</param>
    /// <param name="Rustaveli">This library's, in bytes.</param>
    public sealed record Sizes(int Reference, int Rustaveli);
}
