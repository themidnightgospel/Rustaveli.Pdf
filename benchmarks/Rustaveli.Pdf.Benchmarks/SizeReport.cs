using System.Globalization;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// File size per benchmark document for both libraries. Size is deterministic, so it needs one generation, not a
/// statistical run.
/// </summary>
public static class SizeReport
{
    public static void Print()
    {
        Console.WriteLine("| Document | QuestPDF (bytes) | Rustaveli (bytes) | Ratio |");
        Console.WriteLine("|---|---:|---:|---:|");

        foreach (DocumentKind kind in Enum.GetValues<DocumentKind>())
        {
            int theirs = QuestDocuments.Generate(kind).Length;
            int ours = RustaveliDocuments.Generate(kind).Length;
            string ratio = ((double)ours / theirs).ToString("F2", CultureInfo.InvariantCulture);
            Console.WriteLine($"| {kind} | {theirs:N0} | {ours:N0} | {ratio}× |");
        }
    }
}
