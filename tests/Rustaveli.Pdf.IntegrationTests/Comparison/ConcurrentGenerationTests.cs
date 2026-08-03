using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// Guards the supported concurrency story: many threads generating documents at once must all get identical,
/// uncorrupted output.
/// </summary>
/// <remarks>
/// Skia's PDF backend keeps process-wide font state that concurrent renders interfere with. The symptom is
/// nasty — the file is structurally valid and close to the right size, but its embedded font encoding no longer
/// matches its text operators, so every glyph extracts as U+0000. Nothing throws. Rendering is therefore
/// serialised by default, and this test is what keeps that guarantee honest.
/// </remarks>
public class ConcurrentGenerationTests(ITestOutputHelper output)
{
    private const int Iterations = 24;

    [Fact]
    public void ConcurrentRendersProduceIdenticalDocuments()
    {
        ConcurrentBag<(int Bytes, int Words, string First)> results = new ConcurrentBag<(int Bytes, int Words, string First)>();

        Parallel.For(0, Iterations, _ =>
        {
            byte[] pdf = Recipes.RustaveliTable();
            List<string> words = PdfSnapshot.Capture(pdf).AllWords.ToList();
            results.Add((pdf.Length, words.Count, words.Count > 0 ? words[0] : "<none>"));
        });

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
    }

    [Fact]
    public void SequentialRendersProduceIdenticalDocuments()
    {
        List<byte[]> pdfs = Enumerable.Range(0, Iterations).Select(_ => Recipes.RustaveliTable()).ToList();

        Assert.Single(pdfs.Select(pdf => pdf.Length).Distinct());
    }
}
