<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [5fda522](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/5fda522255ba24a169073a13496aa5d602ec97ba), on 4 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.39 ms | 215 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.75 ms | 359 KB | 14.7 KB |
| Difference | −63% | −40% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 205 ms | 3,464 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 306 ms | 8,188 KB | 950 KB |
| Difference | −33% | −58% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 438 ms | 44,366 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,073 ms | 121,373 KB | 1,042 KB |
| Difference | −59% | −63% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 80.6 ms | 3,053 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,642 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −82% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 4.86 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/5fda522255ba24a169073a13496aa5d602ec97ba/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 33.7 ms |
| Difference | −86% |
