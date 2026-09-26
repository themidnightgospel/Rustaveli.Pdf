// Performance comparison against the QuestPDF oracle (docs/adr/0009-performance-targets.md).
//
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *            every benchmark
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter *Generation*  one class
//     dotnet run --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes               file sizes only

using BenchmarkDotNet.Running;
using Rustaveli.Pdf.Benchmarks;

if (args.Contains("--sizes"))
{
    SizeReport.Print();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(GenerationBenchmarks).Assembly).Run(args);
