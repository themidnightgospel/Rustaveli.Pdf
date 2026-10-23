<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [09737dc](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/09737dcf40c5a091457f6a0b00c1501400c0af0a), on 23 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.31 ms | 115 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.55 ms | 359 KB | 14.7 KB |
| Difference | −63% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 209 ms | 1,456 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 301 ms | 8,187 KB | 950 KB |
| Difference | −31% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 385 ms | 21,036 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 959 ms | 121,367 KB | 1,042 KB |
| Difference | −60% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 81.9 ms | 2,410 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,686 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 8.46 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/09737dcf40c5a091457f6a0b00c1501400c0af0a/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 26.0 ms |
| Difference | −68% |
