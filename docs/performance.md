# Performance

The benchmarks in [`benchmarks/Rustaveli.Pdf.Benchmarks`](https://github.com/themidnightgospel/Rustaveli.Pdf/tree/main/benchmarks/Rustaveli.Pdf.Benchmarks) measure
throughput, allocations, parallel scaling and file size on a fixed set of documents — a one-page invoice, an 85-page
report, a 10,000-row table and a document of images — against the targets in
[ADR 0009](adr/0009-performance-targets.md), which are set relative to a reference library. They run on every pull
request that is ready to merge.

## File size

| Document | Size |
|---|---:|
| Invoice | 6,799 B |
| Report | 171,231 B |
| Large table | 581,890 B |
| Images | 142,133 B |

Fonts are embedded as subsets of the glyphs a document uses, with the hinting PDF viewers ignore left out, and
images are embedded as they were encoded rather than decoded and compressed again. The same documents made by the
reference library are between 1.8 and 25 times larger ([comparisons](questpdf.md#comparisons)).

## Running the benchmarks

```bash
dotnet run -c Release --project benchmarks/Rustaveli.Pdf.Benchmarks -- --filter "*"    # every benchmark
dotnet run -c Release --project benchmarks/Rustaveli.Pdf.Benchmarks -- --sizes         # file sizes only
```
