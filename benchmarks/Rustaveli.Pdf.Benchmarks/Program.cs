// Performance comparison against the QuestPDF oracle (docs/adr/0009-performance-targets.md).
//
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *            every benchmark
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *Generation*  one class
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes               file sizes only
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes sizes.json    and written as JSON

using BenchmarkDotNet.Running;
using Rustaveli.Pdf.Benchmarks;

int sizesFlag = Array.IndexOf(args, "--sizes");

if (sizesFlag >= 0)
{
    SizeReport.Print(sizesFlag + 1 < args.Length ? args[sizesFlag + 1] : null);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(GenerationBenchmarks).Assembly).Run(args);
