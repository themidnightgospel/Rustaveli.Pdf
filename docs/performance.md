# Performance

The benchmarks in [`benchmarks/Rustaveli.Pdf.Benchmarks`](../benchmarks/Rustaveli.Pdf.Benchmarks) measure
throughput, allocations, parallel scaling and file size against QuestPDF 2026.5.0 on a fixed set of documents — a
one-page invoice, an 85-page report, a 10,000-row table and a document of images — against the targets in
[ADR 0009](adr/0009-performance-targets.md). They run on every pull request that is ready to merge.

## File size

| Document | QuestPDF | This library | Ratio |
|---|---:|---:|---:|
| Invoice | 15,011 B | 6,799 B | 0.45× |
| Report | 993,479 B | 171,231 B | 0.17× |
| Large table | 1,063,132 B | 581,890 B | 0.55× |
| Images | 3,581,499 B | 142,133 B | 0.04× |

Fonts are embedded as subsets of the glyphs a document uses, with the hinting PDF viewers ignore left out, and
images are embedded as they were encoded rather than decoded and compressed again.

## Running the benchmarks

```bash
dotnet run -c Release --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter "*"    # every benchmark
dotnet run -c Release --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes         # file sizes only
```
