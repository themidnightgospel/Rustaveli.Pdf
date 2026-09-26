# 0001 — Write PDF ourselves; Skia only for raster output

**Status:** Accepted

## Context
The first backend renders through SkiaSharp's PDF document. It works, but the published SkiaSharp package does not
expose what a full-featured PDF library needs:

- **No font subsetting.** Every typeface is embedded whole; a one-page document is ~570 KB where a subsetting writer
  produces ~32 KB.
- **Process-wide font state.** Concurrent renders corrupt each other's font encodings, silently, so generation holds
  a global lock and cannot scale across cores.
- **No access to tagging, encryption, attachments or document-level structures**, which PDF/UA, PDF/A-3, ZUGFeRD
  invoices and document operations all require.

The alternative that keeps Skia — building our own native Skia with the subsetter and structure APIs exposed —
means maintaining a native build for every platform indefinitely, and still leaves the engine's performance
characteristics fixed.

## Decision
Write PDF output in managed code: object model, content streams, compression, font parsing and subsetting, image
embedding, annotations and document structure. The layout engine already talks to rendering only through `ICanvas`;
the managed writer becomes the implementation behind it.

Skia remains, in an optional package, for what it is genuinely good at: rasterising pages to PNG/JPEG/WebP and
powering the preview tool.

## Consequences
- File size, parallel scaling, tagging and every document-level feature are in our hands.
- The core runs anywhere .NET runs, including trimmed and AOT-compiled apps, with no native binaries.
- We own a spec implementation: correctness must be proven by independent validators (qpdf, veraPDF) and an
  independent renderer, not by our own reader. See [0007](0007-quality-gates.md).
- Font parsing becomes an attack surface for malformed input and must be fuzzed.
