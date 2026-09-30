# How it's tested

```bash
dotnet run eng/tools.cs       # once: fetches pinned, checksum-verified veraPDF, and on Windows qpdf and a Java runtime
                              # (elsewhere: apt/brew install qpdf, and a Java runtime for veraPDF)
dotnet test                   # every suite, on net10.0 and (on Windows) net48
dotnet run eng/coverage.cs    # both suites with coverage, enforcing the floors in eng/coverage-thresholds.json
dotnet stryker                # mutation testing of the engine, failing below 80%
```

Every test runs against both builds the library ships: `net10.0`, and the `netstandard2.0` build on .NET Framework
4.8. Text is measured with the committed Noto Sans (`tests/assets/fonts`), so results are the same on every OS; the
font-fallback tests additionally need CJK and Georgian fonts installed, which Windows and macOS have and Debian or
Ubuntu get from `fonts-noto-core` and `fonts-noto-cjk`. The quality gates each pull request must pass are recorded in
[ADR 0007](adr/0007-quality-gates.md).

**Unit tests** run the layout engine against a deterministic fake type measurer — every character is half the
point size wide, every line exactly the point size tall — and a recording surface that resolves each drawing
operation into absolute page coordinates. This makes expected values calculable by hand and independent of what
fonts happen to be installed. Alongside them, **property-based tests** compose hundreds of random documents and
check invariants no example-based test can cover exhaustively: every character of text is drawn exactly once
across pages, rendering is deterministic, nothing escapes the page, and measuring changes nothing.

**Integration tests** generate real PDFs and read them back with [PdfPig](https://github.com/UglyToad/PdfPig) as
an independent reader. Among them is an equivalence suite that renders identical recipes through this library and
through a reference library, then compares what a reader recovers from each file: page count, per-page word
distribution, word sequence and word positions ([which library, and how they agree](questpdf.md#comparisons)).
Byte-level comparison is not meaningful — two PDF producers never emit identical bytes for the same document — so
the comparison is behavioural throughout. Other integration tests cover font handling, concurrency, and scaling.

**Conformance tests** check a corpus of specimen documents with [qpdf](https://qpdf.readthedocs.io)'s strict
structural validator, and render every page with PDFium — a renderer that shares no code with this library — to
compare against approved snapshots in `tests/Rustaveli.Pdf.ConformanceTests/Snapshots`. A deliberate visual
change is approved with `dotnet run eng/approve-snapshots.cs` after inspecting the received and diff images. The
same specimens are exported as page images through Skia and compared with PDFium's rendering of the PDF: two
renderers that share no code agree within half a percent of pixels. Every specimen is also written as PDF/A-2b,
PDF/A-3u, PDF/A-2a with PDF/UA-1, and PDF/UA-1 alone, and each file checked against every standard it claims by
[veraPDF](https://verapdf.org), the reference validator for both.

**Mutation testing** runs over the whole engine every night: Stryker.NET changes the code in small ways, and each
area must have tests that catch at least 80% of the changes.

**Fuzzing** feeds the readers — fonts, PNG and JPEG images, SVG and PDF — inputs no one would write by hand.
[libFuzzer](https://llvm.org/docs/LibFuzzer.html), through [SharpFuzz](https://github.com/Metalnem/sharpfuzz), mutates
real files and keeps each mutation that reaches code no input reached before. An input fails when a reader throws
anything it does not document, hangs, or asks for memory no file warrants; files the library writes from what it read
must also read back without any exception at all. It runs for a few minutes on each pull request that touches the
library, and for longer every night; see [`fuzz/README.md`](https://github.com/themidnightgospel/Rustaveli.Pdf/tree/main/fuzz)
for running it locally and replaying a failure.

**Benchmarks** measure speed, allocations and file size; see [performance](performance.md).

