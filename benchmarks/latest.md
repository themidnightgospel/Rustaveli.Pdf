<!-- benchmarks-questpdf -->
### Benchmarks against QuestPDF

Measured on main as of its latest merge, [c02a468](https://github.com/themidnightgospel/Rustaveli.Pdf/commit/c02a4680f1c0997f5f00224c0b3d6379fa787cbd), on 2 October 2026, on a GitHub-hosted runner. Every merge's numbers are charted in the [benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).

Both libraries make the same documents in the same run. For each figure smaller is better; the difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.

#### Invoice

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L29-L41) | 1.46 ms | 274 KB | 6.64 KB |
| [QuestPDF](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L47-L59) | 3.75 ms | 359 KB | 14.7 KB |
| Difference | −61% | −24% | −55% |

#### Report

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L43-L50) | 209 ms | 3,746 KB | 172 KB |
| [QuestPDF](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L61-L68) | 307 ms | 8,188 KB | 950 KB |
| Difference | −32% | −54% | −82% |

#### LargeTable

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L52-L54) | 483 ms | 71,183 KB | 568 KB |
| [QuestPDF](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L70-L72) | 1,072 ms | 121,367 KB | 1,042 KB |
| Difference | −55% | −41% | −45% |

#### Images

| | Time | Allocated | File size |
|---|---:|---:|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/RustaveliDocuments.cs#L56-L67) | 83.0 ms | 3,085 KB | 139 KB |
| [QuestPDF](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/QuestDocuments.cs#L74-L86) | 1,640 ms | 17,171 KB | 3,498 KB |
| Difference | −95% | −82% | −96% |

#### Eight documents in parallel

| | Time |
|---|---:|
| [Rustaveli.Pdf](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L32) | 5.19 ms |
| [QuestPDF](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/c02a4680f1c0997f5f00224c0b3d6379fa787cbd/benchmarks/Rustaveli.Pdf.Benchmarks/ParallelBenchmarks.cs#L26) | 34.1 ms |
| Difference | −85% |
