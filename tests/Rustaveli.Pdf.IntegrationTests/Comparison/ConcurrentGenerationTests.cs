using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// Guards the supported concurrency story: many threads generating documents at once must all get identical,
/// uncorrupted output.
/// </summary>
/// <remarks>
/// Typefaces, their parsed tables and the caches built from them are shared by every export in the process, so a
/// race among them would show here. The symptom to fear is quiet — a file that is structurally valid and close to
/// the right size, but whose font no longer matches its text, so its words extract wrongly. Nothing throws.
/// </remarks>
public class ConcurrentGenerationTests(ITestOutputHelper output)
{
    private const int Iterations = 24;

    [Fact]
    public void ConcurrentRendersProduceIdenticalDocuments()
    {
        byte[] expected = Recipes.RustaveliTable();
        ConcurrentBag<(int Bytes, int Words, string First)> results = new ConcurrentBag<(int Bytes, int Words, string First)>();
        ConcurrentBag<string> kept = new ConcurrentBag<string>();

        Parallel.For(0, Iterations, _ =>
        {
            byte[] pdf = Recipes.RustaveliTable();
            List<string> words = PdfSnapshot.Capture(pdf).AllWords.ToList();
            results.Add((pdf.Length, words.Count, words.Count > 0 ? words[0] : "<none>"));

            // A render that differs is kept, so that a failure too rare to reproduce can still be read.
            if (!pdf.AsSpan().SequenceEqual(expected))
            {
                string path = Path.Combine(Path.GetTempPath(), $"concurrent-render-{Guid.NewGuid():N}.pdf");
                File.WriteAllBytes(path, pdf);
                kept.Add(path);
            }
        });

        if (!kept.IsEmpty)
            output.WriteLine($"Renders unlike the one drawn alone ({expected.Length} bytes): {string.Join(", ", kept)}");

        List<int> byteSizes = results.Select(result => result.Bytes).Distinct().OrderBy(size => size).ToList();
        List<int> wordCounts = results.Select(result => result.Words).Distinct().OrderBy(count => count).ToList();
        List<string> firstWords = results.Select(result => result.First).Distinct().ToList();

        output.WriteLine($"byteSizes=[{string.Join(",", byteSizes)}] " +
                         $"wordCounts=[{string.Join(",", wordCounts)}] " +
                         $"firstWords=[{string.Join("|", firstWords)}]");

        // Differing byte sizes would mean the corruption happened while writing, not while reading.
        Assert.True(byteSizes.Count == 1, $"Concurrent renders produced {byteSizes.Count} distinct file sizes: {string.Join(", ", byteSizes)}");
        Assert.True(wordCounts.Count == 1, $"Concurrent renders produced {wordCounts.Count} distinct word counts: {string.Join(", ", wordCounts)}");
        Assert.True(firstWords is ["Code"], $"Concurrent renders disagreed on the first word: {string.Join(" | ", firstWords)}");
        Assert.True(kept.IsEmpty, $"{kept.Count} concurrent renders differ from the one drawn alone, kept at: {string.Join(", ", kept)}");
    }

    [Fact]
    public void SequentialRendersProduceIdenticalDocuments()
    {
        List<byte[]> pdfs = Enumerable.Range(0, Iterations).Select(_ => Recipes.RustaveliTable()).ToList();

        Assert.Single(pdfs.Select(pdf => pdf.Length).Distinct());
    }
}
