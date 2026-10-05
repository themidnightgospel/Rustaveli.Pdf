<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [c202b8c](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/c202b8ca6b4acd0dfc94475c6fd672578220f2b9), on 5 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.37 ms | 159 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.69 ms | 359 KB | 14.7 KB |
| Difference | −63% | −56% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 203 ms | 1,805 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 307 ms | 8,188 KB | 950 KB |
| Difference | −34% | −78% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 397 ms | 27,135 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,057 ms | 121,371 KB | 1,042 KB |
| Difference | −62% | −78% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 82.5 ms | 2,500 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,638 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −85% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 4.79 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c202b8ca6b4acd0dfc94475c6fd672578220f2b9/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 31.9 ms |
| Difference | −85% |
