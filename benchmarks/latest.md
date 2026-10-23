<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [ab82532](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/ab825321e56364f738a2d9c6283fba6934081685), on 23 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.31 ms | 115 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.72 ms | 359 KB | 14.7 KB |
| Difference | −65% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 202 ms | 1,456 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 307 ms | 8,187 KB | 950 KB |
| Difference | −34% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 371 ms | 21,036 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,054 ms | 121,367 KB | 1,042 KB |
| Difference | −65% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 80.4 ms | 2,410 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,643 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 4.55 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/ab825321e56364f738a2d9c6283fba6934081685/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 32.9 ms |
| Difference | −86% |
