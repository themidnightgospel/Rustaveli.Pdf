// Performance comparison against the QuestPDF oracle (docs/adr/0009-performance-targets.md).
//
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *            every benchmark
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *Generation*  one class
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes               file sizes only
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes sizes.json    and written as JSON

using BenchmarkDotNet.Running;
using Rustaveli.Pdf.Benchmarks;

int sizes = Array.IndexOf(args, "--sizes");

if (sizes >= 0)
{
    SizeReport.Print(sizes + 1 < args.Length ? args[sizes + 1] : null);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(GenerationBenchmarks).Assembly).Run(args);
