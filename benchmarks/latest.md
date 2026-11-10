<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [6353214](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/63532143c07e1a0463c7c28549bb3e2e798ce89b), on 10 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L31-L43) | 1.30 ms | 114 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L48-L60) | 3.66 ms | 359 KB | 14.7 KB |
| Difference | −65% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L45-L52) | 202 ms | 1,441 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L62-L69) | 306 ms | 8,188 KB | 950 KB |
| Difference | −34% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L54-L56) | 380 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L71-L73) | 1,021 ms | 121,367 KB | 1,042 KB |
| Difference | −63% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L58-L70) | 82.0 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L75-L88) | 1,647 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 7.46 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/63532143c07e1a0463c7c28549bb3e2e798ce89b/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 33.1 ms |
| Difference | −77% |
