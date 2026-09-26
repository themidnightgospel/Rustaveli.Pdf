# 0006 — A dependency-free core; native code only by opt-in

**Status:** Accepted

## Context
Every native dependency narrows where a library can run (sandboxed hosts, WASM, unusual architectures) and adds a
supply-chain and packaging burden. Some capabilities, though, are only realistic with native code: complex-script
shaping (HarfBuzz) and high-quality rasterisation (Skia).

## Decision
| Package | Contents | Dependencies |
|---|---|---|
| `Rustaveli.Pdf` | layout, PDF writer, font subsetting, basic OpenType shaping (kerning, standard ligatures) | none |
| `Rustaveli.Pdf.Shaping` | complex scripts (Arabic, Indic, Thai), bidi | HarfBuzzSharp |
| `Rustaveli.Pdf.Raster` | pages as PNG/JPEG/WebP | SkiaSharp |
| `Rustaveli.Pdf.Svg` | SVG to vector PDF | none |
| `Rustaveli.Pdf.Operations` | merge, overlay, attachments, encryption | none |
| `Rustaveli.Pdf.Preview` | developer preview tool | — |

Basic shaping covers Latin, Cyrillic, Greek, Georgian and CJK without native code.

## Consequences
- The default install is pure managed code and runs everywhere .NET does.
- Complex-script users add one package; the text engine must expose a shaping seam that package plugs into.
