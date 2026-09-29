# Features

Everything the library does, by area. The [guides](guide/README.md) show how to use each, and the
[parity checklist](parity/PARITY.md) maps them to QuestPDF's capabilities.

**Layout** — sections with running heads and feet, underlays and overlays, fixed and continuous pages, margins,
`Columns` (fixed, shared and natural widths, right-to-left), `Stack`, `Table` (fixed and shared columns, automatic
and explicit cell placement, row and column spanning, header and footer rows repeated on every page, pagination at
row boundaries, last cells extended to the bottom), `List` (bulleted, numbered, lettered, roman — numbering survives
new pages), `Layered`, `Banded`, `Grid`, `Flow` (items wrapping as words do) and `FlowColumns` (newspaper columns,
balanced or not); content composed per page from state, or only when layout reaches it; draw order across the page;
named type, paragraph and frame styles in a style sheet, each able to build on another.

**Modifiers** — insets, fills and strokes in inks or gradients, stroke alignment, rounded corners alike or each on
its own, drop shadows, width and height constraints, expansion, fitting to content, proportion, flush and centred
placement, shifting, scaling, shrink-to-fit, rotation by any angle, quarter turns, mirroring.

**Text** — styled runs, weight, italic, ink, highlight, underline, strike-through and overline (solid, double,
dotted, dashed or wavy, in their own ink and weight), leading, tracking, word spacing, subscript and superscript;
paragraphs flush left, right, start or end, centred or justified, broken into lines by the Unicode rules (UAX #14)
or anywhere, limited to a number of lines with an ellipsis, with non-breaking spaces, first-line indents, space
between paragraphs and inline frames placed against the line; flow across pages, folios in any numerals, page
counts, cross-references and page numbers within anchored content, and default type inherited from the section.

**Flow** — `When` (a condition, or the pages a condition accepts), `Once`, `SkipFirst`, `KeepTogether`,
`KeepTogetherWherePossible`, `RequireSpace`, `NewPage`, `RepeatOnEachPage`, `DiscardOverset`, a document-wide page
limit, and reusable `ISnippet`s.

**Ink** — RGB, CMYK process colour and named spot inks with a process fallback, tints and opacity
([ADR 0004](adr/0004-ink-colour-model.md)). There is no built-in palette: a document brings its own colours.

**Output** — PDF written in managed code: TrueType and CFF faces subset to the glyphs used — a Chinese face's 16 MB
to a few kilobytes — each searchable through a ToUnicode map; JPEGs and most PNGs embedded as they were encoded, with palettes, alpha,
colour keys, sixteen bits and ICC profiles kept, images shared by content and turned upright by their EXIF
orientation; CMYK process colour, spot inks as separations with a process fallback, and opacity; links,
cross-references to anchors, bookmarks and document information. Exports run in parallel. PDF/A-2 and PDF/A-3 at
levels B, U and A; tagged PDF and PDF/UA-1, with headings, lists, tables, figures and their alternative text, and
languages; password protection from RC4 to AES-256 — all checked by veraPDF and qpdf in the tests. Page images —
PNG, JPEG or WebP at any resolution — SVG pages and XPS come from the `Rustaveli.Pdf.Raster` package, drawn from the
same layout and glyphs.

**Images and artwork** — JPEG and PNG images fitted four ways, recompressed and scaled to their shown size on
request; vector artwork read from SVG or drawn from paths, text and images, and kept vector in the PDF; images and
artwork made for the exact box they fill.

**Existing files** — the `Rustaveli.Pdf.Operations` package reads PDF files in managed code, repairing what it can,
and keeps and reorders pages, appends other files, lays them over or under as stamps and letterheads, attaches files
for PDF/A-3 and electronic invoices, extends the metadata, protects and unprotects, and linearises for the web.

**Typefaces** — a `TypefaceLibrary` of registered and installed typefaces, matched by weight and slant, with fallback
typefaces per style and per library and per-character fallback for anything a face lacks; OpenType substitutions
(ligatures, small capitals, figure styles and any feature by tag) and pair kerning; text in both directions ordered
by the Unicode bidirectional algorithm; substitution for a typeface nobody has; Noto Sans carried in the package and
set by default, so a document that names no typeface looks the same on every machine and one with no fonts at all
still has type; and an optional check that every glyph exists.
Complex scripts — Arabic, Hebrew points, Indic and South-East Asian scripts — are shaped by HarfBuzz once the
`Rustaveli.Pdf.Shaping` package is added and `ShapeComplexScripts()` called on the library.

**Preview** — `DocumentPreview.Preview(Compose)` from the `Rustaveli.Pdf.Preview` package shows a document in the
browser and draws it again whenever `dotnet watch` applies a code change; a layout failure is shown in its place,
traced down to the frame that could not fit. Press <kbd>I</kbd> to inspect: the frame under the pointer is
outlined, every frame drawn on the page is listed within the one that drew it, and each opens the line of code that
made it in the editor. `ShowFrameEdges` and `Named` mark frames on the page itself.

