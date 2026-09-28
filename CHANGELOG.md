# Changelog

Versions follow [semantic versioning](https://semver.org/): nothing public changes incompatibly within a major
version.

## 1.0.0

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
OpenType features and kerning, and Noto Sans carried in the package.

**Images and artwork** — JPEG and PNG embedded as encoded, SVG and drawn artwork kept vector, and images and
artwork made for the box they fill.

**Output** — PDF with subset fonts, their hinting left out unless kept, CMYK and spot inks, links and bookmarks; PDF/A-2 and PDF/A-3 at levels B, U and
A; tagged PDF and PDF/UA-1; password protection; page images, SVG and XPS. Pages render in parallel, and a document
can be exported from many threads at once.

**Existing files** — merging, reordering, stamping and letterheads, attachments for electronic invoices, metadata,
protection and linearisation.

**Tooling** — the live preview and its inspector, frame edges and names, and layout failures traced to the frame
that did not fit.
