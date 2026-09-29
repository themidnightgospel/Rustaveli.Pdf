# Changelog

Versions follow [semantic versioning](https://semver.org/). Before 1.0 the public surface may still change from one
minor version to the next; from 1.0, nothing public changes incompatibly within a major version.

## Unreleased

**Fixed**

- A table cell pinned to a row with no room left in it covered a cell already there; it is now refused with a
  `CompositionException` naming the row.
- Layout error messages, and `Extent` and `Offset` as text, wrote numbers in the current culture — `0,000` on a
  machine that writes decimals with a comma. They read the same in every culture now.
- A running head or foot of no height was not drawn at all, so an anchor, bookmark or marker in it never took effect
  and links to it went nowhere.
- SVG artwork with numbers beyond what a PDF can hold (10^15), or transforms, clips, gradients and view boxes that
  reach beyond it, failed the export; what reaches beyond it is left out. `ArtworkComposer` refuses such numbers
  when they are given.
- SVG path data with anything but a command after a close looped forever.
- An SVG opacity above 1 failed; opacities are clamped, as SVG says.
- Reading a PDF: a number with two decimal points, a cross-reference stream without a usable `/W` or `/Size`, an
  AES-256 file whose wrapped key is not 32 bytes, and an RC4 or AES-128 file with a key outside 40 to 128 bits or an
  owner entry shorter than 32 bytes threw exceptions `PdfFile.Open` does not document. The first two are repaired;
  the others are an `UnreadableFileException`.

**Changed**

- `PdfFile.Open` documents the `NotSupportedException` it throws for a security handler other than the standard one.

## 0.1.0

The first release: every capability of QuestPDF 2026.5.0, its last MIT release, in a vocabulary drawn from print —
see the [parity checklist](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/docs/parity/PARITY.md) and
the [guides](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/docs/guide/README.md).

**Packages**

- `Rustaveli.Pdf` — layout, text and PDF export, in managed code with no native dependencies.
- `Rustaveli.Pdf.Raster` — page images, SVG pages and XPS, and image recompression, through SkiaSharp.
- `Rustaveli.Pdf.Shaping` — Arabic, Hebrew points, and Indic and South-East Asian scripts, through HarfBuzz.
- `Rustaveli.Pdf.Operations` — reading and changing existing PDF files.
- `Rustaveli.Pdf.Preview` — a live preview in the browser, redrawn on hot reload, with an inspector.

Each targets .NET 10 and .NET Standard 2.0.

**Layout** — sections with running heads and feet, underlays and overlays; stacks, columns, tables with repeated
header and footer rows and spanning cells, lists, layers, bands, grids, flows and flowing columns; insets, fills,
strokes, gradients, rounded corners, drop shadows, size constraints, placement, rotation, scaling and mirroring;
keeping content together, conditions on the page, repetition, and content composed per page; named styles.

**Text** — styled runs, paragraphs aligned, justified and indented, lines broken by the Unicode rules, text in both
directions, folios, page counts and cross-references; typefaces registered or installed, fallback per character,
OpenType features and kerning, and Noto Sans carried in the package as the default typeface.

**Images and artwork** — JPEG and PNG embedded as encoded, SVG and drawn artwork kept vector, and images and
artwork made for the box they fill.

**Output** — PDF with TrueType and CFF fonts subset, their hinting left out unless kept, CMYK and spot inks, links and bookmarks; PDF/A-2 and PDF/A-3 at levels B, U and
A; tagged PDF and PDF/UA-1; password protection; page images, SVG and XPS. Pages render in parallel, and a document
can be exported from many threads at once.

**Existing files** — merging, reordering, stamping and letterheads, attachments for electronic invoices, metadata,
protection and linearisation.

**Tooling** — the live preview and its inspector, frame edges and names, and layout failures traced to the frame
that did not fit.
