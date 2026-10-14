<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [eae3a61](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/eae3a61ba36368ef4090baeda3aa4ce82c6a8852), on 14 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.30 ms | 122 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.76 ms | 359 KB | 14.7 KB |
| Difference | −65% | −66% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 209 ms | 1,476 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 307 ms | 8,188 KB | 950 KB |
| Difference | −32% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 376 ms | 22,371 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,047 ms | 121,369 KB | 1,042 KB |
| Difference | −64% | −82% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 82.3 ms | 2,415 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,640 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 4.57 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/eae3a61ba36368ef4090baeda3aa4ce82c6a8852/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 34.1 ms |
| Difference | −87% |
