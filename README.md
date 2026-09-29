# Rustaveli.Pdf

A free, open source, fluent PDF generation library for .NET.

> **Version 0.1, the first release.** Every capability of QuestPDF's last MIT release, tooling included — see the
> [parity checklist](docs/parity/PARITY.md) — on `net10.0` and `netstandard2.0` (.NET Framework 4.7.2+). The
> [guides](docs/guide/README.md) show how to use it.

## Why this exists

QuestPDF popularised the fluent, composable approach to building PDFs in .NET. As of release **2026.6.0**
(June 2026) it moved from an MIT grant to a source-available commercial licence that is explicitly not
OSI-approved, excludes public-sector bodies and publicly traded companies from its free tier regardless of
revenue, and gates everyone else behind a $1M annual revenue threshold.

This project provides the same category of tool under a genuinely permissive licence, with no revenue gate and
no eligibility classes.

It is written from scratch rather than forked. [FossPDF](https://github.com/lol768/FossPDF.NET) already
continues QuestPDF's MIT-era code and is the shorter path to a free library; this codebase exists to be a fresh
design, in a vocabulary of its own drawn from print and typesetting ([glossary](docs/GLOSSARY.md)), with the layout
engine kept strictly behind a drawing-surface seam so the output backend remains a replaceable decision rather than
a foundational one.

## Quick start

```csharp
using Rustaveli.Pdf;

(string Code, string Description, string Amount)[] rows =
[
    ("A-100", "Consulting", "1,200.00"),
    ("B-200", "Licences", "300.00"),
];

Document document = Document.Compose(composition => composition.Section(section =>
{
    section.Trim = PaperSizes.A4;
    section.Margins = Sides.All(40);
    section.DefaultType = TypeStyle.Default.WithTypeface("Noto Sans").WithPointSize(11);

    section.RunningHead().Text("Quarterly Statement");

    section.RunningFoot().Centered().Text(text =>
    {
        text.Run("Page ");
        text.Folio();
        text.Run(" of ");
        text.PageCount();
    });

    section.Body().Table(table =>
    {
        table.Columns(columns =>
        {
            columns.Fixed(90);
            columns.Share();
            columns.Fixed(70);
        });

        table.HeaderRows(header =>
        {
            header.Cell().Text("Code");
            header.Cell().Text("Description");
            header.Cell().FlushRight().Text("Amount");
        });

        foreach ((string code, string description, string amount) in rows)
        {
            table.Cell().Text(code);
            table.Cell().Text(description);
            table.Cell().FlushRight().Text(amount);
        }
    });
}));

document.ExportPdf("statement.pdf");
```

A document is composed of sections; each section has a running head, a body and a running foot, and every frame
you set content into can be modified first — inset, filled, stroked, placed, turned — before the content ends the
chain. The [glossary](docs/GLOSSARY.md) lists every word the API uses and where it comes from. This example is
compiled and run by `QuickStartTests`, so it stays correct.

## Documentation

The [guides](docs/guide/README.md) go through the library task by task — [getting started](docs/guide/getting-started.md),
[layout](docs/guide/layout.md), [text](docs/guide/text.md), [images and artwork](docs/guide/images-and-artwork.md),
[output](docs/guide/output.md), [existing files](docs/guide/existing-files.md) and
[preview and debugging](docs/guide/preview-and-debugging.md) — and [coming from QuestPDF](docs/guide/coming-from-questpdf.md)
maps its names to these. Every example in them is compiled by the tests, as this README's is. The packages carry
XML documentation for every public member, and the [glossary](docs/GLOSSARY.md) lists every name the API uses.

## Architecture

The library is split so that layout never depends on how pixels or PDF operators are produced.

```
Composition           Document → sections → frames; modifiers and composers for text, stacks, columns, tables
      │
      ▼
Blocks                The tree the composition builds — decorators, arrangements, leaf content
      │
      ▼
Typesetter            Plan(space) → Fit, then Render(space); pagination; repeated passes for page counts
      │
      ▼
ISurface              The single seam to any backend, fed glyphs by one shaper for measuring and drawing alike
      │
      ├─▶ PDF             the managed writer: Type 0 font subsets, images as encoded, separations
      └─▶ Page images     Rustaveli.Pdf.Raster: PNG, JPEG or WebP through SkiaSharp

      Complex scripts   Rustaveli.Pdf.Shaping: HarfBuzz behind the same shaper, by opt-in
```

Only the composition layer is public. Blocks, the typesetter and the drawing seam are internal, so the engine can
change without breaking anyone.

### The fitting contract

Every block answers one question before anything is drawn: *given this much space, what would you do?* The answer
is a `Fit` with four outcomes.

| Outcome | Meaning |
|---|---|
| `Nothing` | Nothing left to set. A continuation page must not give it space again. |
| `Defer` | Cannot be set here at all — defer to the next page. |
| `Partial` | Sets what fits; the rest continues on the next page. |
| `Complete` | Sets everything. |

Pagination falls out of this. The typesetter responds to `Defer` by starting a fresh page and retrying — but if a
block defers on a page that is *already empty*, no further page could ever help, so it raises `OversetException`
rather than looping forever.

Blocks must honour one rule: **`Render` may only use the space `Plan` promised for the same input**, and a parent
allots each child its final size ([ADR 0012](docs/adr/0012-parents-allot-final-size.md)). `Plan` must be free of
side effects, because the typesetter plans speculatively and discards results.

### Counting passes

Content such as "Page 3 of 12" cannot resolve in a single pass, because the page count is unknown until
pagination finishes. The typesetter therefore sets the document more than once — first to a page sink that
discards everything, purely to count pages, repeating until the count settles, then for real. Every pass runs
identical layout code, so the count cannot drift.

### Per-page state

Running heads, running feet and repeating table rows are set in full on every page, but their *content* tracks how
much of itself it has set. These get a softer reset between pages that clears pagination progress while preserving
document-wide state, so a `Once` frame inside a running head still appears only once. Getting this wrong is what
made table header rows silently vanish after page one — a defect the QuestPDF comparison suite caught.

## What's implemented

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
([ADR 0004](docs/adr/0004-ink-colour-model.md)). There is no built-in palette: a document brings its own colours.

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

## Testing

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
[ADR 0007](docs/adr/0007-quality-gates.md).

**Unit tests** run the layout engine against a deterministic fake type measurer — every character is half the
point size wide, every line exactly the point size tall — and a recording surface that resolves each drawing
operation into absolute page coordinates. This makes expected values calculable by hand and independent of what
fonts happen to be installed. Alongside them, **property-based tests** compose hundreds of random documents and
check invariants no example-based test can cover exhaustively: every character of text is drawn exactly once
across pages, rendering is deterministic, nothing escapes the page, and measuring changes nothing.

**Integration tests** generate real PDFs and read them back with [PdfPig](https://github.com/UglyToad/PdfPig) as
an independent reader. Among them is an equivalence suite that renders identical recipes through this library and
through QuestPDF, then compares what a reader recovers from each file: page count, per-page word distribution,
word sequence and word positions. Others cover font handling, concurrency, and scaling.

**Conformance tests** check a corpus of specimen documents with [qpdf](https://qpdf.readthedocs.io)'s strict
structural validator, and render every page with PDFium — a renderer that shares no code with this library — to
compare against approved snapshots in `tests/Rustaveli.Pdf.ConformanceTests/Snapshots`. A deliberate visual
change is approved with `dotnet run eng/approve-snapshots.cs` after inspecting the received and diff images. The
same specimens are exported as page images through Skia and compared with PDFium's rendering of the PDF: two
renderers that share no code agree within half a percent of pixels. Every specimen is also written as PDF/A-2b,
PDF/A-3u, PDF/A-2a with PDF/UA-1, and PDF/UA-1 alone, and each file checked against every standard it claims by
[veraPDF](https://verapdf.org), the reference validator for both.

**Benchmarks** (`benchmarks/Rustaveli.Pdf.Benchmarks`) measure throughput, allocations, parallel scaling and file
size against QuestPDF on a fixed set of documents, against the targets in
[ADR 0009](docs/adr/0009-performance-targets.md). File sizes on those documents:

| Document | QuestPDF | This library | Ratio |
|---|---:|---:|---:|
| Invoice | 15,011 B | 6,799 B | 0.45× |
| Report | 993,479 B | 171,231 B | 0.17× |
| Large table | 1,063,132 B | 581,890 B | 0.55× |
| Images | 3,581,499 B | 142,133 B | 0.04× |

Current agreement across text flow, header/footer pagination and multi-page tables: **identical page counts,
identical word sequences, identical horizontal word positions and identical line spacing, with every line 0.24pt
higher on the page** — a constant offset in where the first baseline falls below the top of the text area.

Byte-level comparison is not meaningful — two PDF producers never emit identical bytes for the same document —
so the comparison is behavioural throughout.

> **On the QuestPDF test dependency.** The oracle is pinned to **2026.5.0**, the last release distributed under
> an MIT grant. Releases from 2026.6.0 onwards carry a licence whose restrictions forbid using the software to
> develop a competing PDF library, which would cover this test suite. Do not upgrade this reference.

## Known limitations

**Complex scripts need the Shaping package.** Without `Rustaveli.Pdf.Shaping`, Arabic, Hebrew points and Indic and
South-East Asian scripts are set without their shaping rules: letters unjoined, marks unplaced.

**SVG is read as drawing tools write it.** Radial gradients are drawn in the mean of their colours, and filters,
masks, patterns and markers are left out.

**XPS is written on Windows only**, as it relies on the platform's XPS support.

## Roadmap

The first release delivers every capability of QuestPDF — including its tooling — in a vocabulary of our own. The
[parity checklist](docs/parity/PARITY.md) tracks each capability against the phase that delivered it, and the
[architecture decision records](docs/adr/README.md) explain the choices behind the plan. Every phase below is done.

| Phase | Delivered |
|---|---|
| 0 | Groundwork: quality gates, pipelines, conformance and property tests, benchmarks |
| 1 | The print and typesetting vocabulary ([ADR 0002](docs/adr/0002-print-vocabulary.md)); `Ink` colour |
| 2 | A managed PDF writer with font subsetting and parallel rendering |
| 3 | The text engine: line breaking, justification, fallback, OpenType features, complex scripts |
| 4 | Layout and styling parity, and named style sheets |
| 5 | Images and SVG |
| 6 | Output formats and conformance: page images, CMYK and spot colour, PDF/A, PDF/UA |
| 7 | Document operations: merge, overlay, attachments, encryption |
| 8 | A live preview tool with hot reload |
| 9 | Documentation, and the first release |

## Licence

MIT. See [LICENSE](LICENSE).

The core package carries Latin, Greek and Cyrillic subsets of [Noto Sans](https://notofonts.github.io/), set for
text in Noto Sans where no Noto Sans is registered or installed, and for text nothing registered or installed can
set. They are distributed under the SIL Open Font License
1.1, which travels with them in the package (`licenses/NotoSans-OFL.txt`) and is in
[`src/Rustaveli.Pdf/Fonts/Bundled`](src/Rustaveli.Pdf/Fonts/Bundled/OFL.txt).

This project is independent and is not affiliated with, endorsed by, or sponsored by QuestPDF.
