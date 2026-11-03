<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [428d658](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/428d658f97086243b115336a2e5d0215853b3a47), on 3 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.02 ms | 114 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 2.76 ms | 359 KB | 14.7 KB |
| Difference | −63% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 162 ms | 1,441 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 240 ms | 8,190 KB | 950 KB |
| Difference | −32% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 304 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 733 ms | 121,371 KB | 1,042 KB |
| Difference | −59% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 62.8 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,315 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 4.17 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/428d658f97086243b115336a2e5d0215853b3a47/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 19.7 ms |
| Difference | −79% |
