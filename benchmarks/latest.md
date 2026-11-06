<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF 2026.5.0 (the last MIT-licensed release)

Measured on main as of its latest merge, [734fbf2](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/734fbf2cc0076c483c9811b7551870e3c53ce4b8), on 6 November 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.43 ms | 114 KB | 6.64 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.66 ms | 359 KB | 14.7 KB |
| Difference | −61% | −68% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 201 ms | 1,441 KB | 172 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 302 ms | 8,188 KB | 950 KB |
| Difference | −34% | −82% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 376 ms | 20,784 KB | 568 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,054 ms | 121,371 KB | 1,042 KB |
| Difference | −64% | −83% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 81.6 ms | 2,406 KB | 139 KB |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,646 ms | 17,170 KB | 3,498 KB |
| Difference | −95% | −86% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L24) | 4.42 ms |
| [QuestPDF 2026.5.0](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/734fbf2cc0076c483c9811b7551870e3c53ce4b8/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L18) | 32.2 ms |
| Difference | −86% |
