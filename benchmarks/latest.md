<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [bbbf57e](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f), on 23 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L31-L43) | 1.75 ms | 115 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L48-L60) | 3.70 ms | 359 KB | 14.7 KB |
| Difference | −53% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L45-L52) | 206 ms | 1,442 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L62-L69) | 308 ms | 8,188 KB | 950 KB |
| Difference | −33% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L54-L56) | 371 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L71-L73) | 1,037 ms | 121,370 KB | 1,042 KB |
| Difference | −64% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L58-L70) | 82.7 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L75-L88) | 1,641 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 8.53 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/bbbf57ef2e973ecf1ed1ad8fbbc40ffc938da32f/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 34.2 ms |
| Difference | −75% |
