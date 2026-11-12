<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [46d0ce9](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0), on 12 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L31-L43) | 1.00 ms | 114 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L48-L60) | 2.67 ms | 359 KB | 14.7 KB |
| Difference | −62% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L45-L52) | 159 ms | 1,441 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L62-L69) | 235 ms | 8,189 KB | 950 KB |
| Difference | −32% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L54-L56) | 297 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L71-L73) | 747 ms | 121,371 KB | 1,042 KB |
| Difference | −60% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L58-L70) | 62.9 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L75-L88) | 1,315 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 5.99 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/46d0ce9f8b5d211e9ff1e0e6c71de804e15bd9c0/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 20.9 ms |
| Difference | −71% |
