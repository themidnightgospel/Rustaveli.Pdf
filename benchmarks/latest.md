<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [de9261f](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/de9261f7523efe169a79e6339900a1b580882d39), on 11 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L31-L43) | 0.98 ms | 114 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L48-L60) | 2.66 ms | 359 KB | 14.7 KB |
| Difference | −63% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L45-L52) | 160 ms | 1,442 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L62-L69) | 234 ms | 8,189 KB | 950 KB |
| Difference | −32% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L54-L56) | 295 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L71-L73) | 731 ms | 121,372 KB | 1,042 KB |
| Difference | −60% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L58-L70) | 62.7 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L75-L88) | 1,321 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 4.50 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/de9261f7523efe169a79e6339900a1b580882d39/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 19.6 ms |
| Difference | −77% |
