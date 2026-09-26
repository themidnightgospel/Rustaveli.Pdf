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

    [GlobalSetup]
    public void Setup()
    {
        // Pay for font registration and first-use initialisation outside the measured region.
        BenchmarkFonts.EnsureRegistered();
        _ = QuestDocuments.Generate(DocumentKind.Invoice);
        _ = RustaveliDocuments.Generate(DocumentKind.Invoice);
    }

    [Benchmark(Baseline = true)]
    public int QuestPdf() => QuestDocuments.Generate(Kind).Length;

    [Benchmark]
    public int Rustaveli() => RustaveliDocuments.Generate(Kind).Length;
}
