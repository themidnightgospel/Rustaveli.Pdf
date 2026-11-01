using BenchmarkDotNet.Attributes;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// Eight invoices generated one after another versus all at once. The ratio between the pairs is the parallel
/// scaling in ADR 0009 (target: eight in parallel at least six times single-document throughput).
/// </summary>
[MemoryDiagnoser]
public class ParallelBenchmarks
{
    private const int Documents = 8;

    [Benchmark(Baseline = true)]
    public int QuestPdfSequential() => OneAfterAnother(QuestDocuments.Generate);

    [Benchmark]
    public int QuestPdfParallel() => AllAtOnce(QuestDocuments.Generate);

    [Benchmark]
    public int RustaveliSequential() => OneAfterAnother(RustaveliDocuments.Generate);

    [Benchmark]
    public int RustaveliParallel() => AllAtOnce(RustaveliDocuments.Generate);

    private static int OneAfterAnother(Func<DocumentKind, byte[]> generate)
    {
        int total = 0;
        for (int index = 0; index < Documents; index++)
            total += generate(DocumentKind.Invoice).Length;
        return total;
    }

    private static int AllAtOnce(Func<DocumentKind, byte[]> generate)
    {
        int[] sizes = new int[Documents];
        Parallel.For(0, Documents, index => sizes[index] = generate(DocumentKind.Invoice).Length);
        return sizes.Sum();
    }
}
