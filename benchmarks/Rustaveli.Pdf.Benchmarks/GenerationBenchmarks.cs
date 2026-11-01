using BenchmarkDotNet.Attributes;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// Single-threaded generation of each benchmark document by both libraries: throughput and allocations. QuestPDF is
/// the baseline, so the Ratio column reads directly against the targets in ADR 0009.
/// </summary>
[MemoryDiagnoser]
public class GenerationBenchmarks
{
    [Params(DocumentKind.Invoice, DocumentKind.Report, DocumentKind.LargeTable, DocumentKind.Images)]
    public DocumentKind Kind { get; set; }

    [Benchmark(Baseline = true)]
    public int QuestPdf() => QuestDocuments.Generate(Kind).Length;

    [Benchmark]
    public int Rustaveli() => RustaveliDocuments.Generate(Kind).Length;
}
