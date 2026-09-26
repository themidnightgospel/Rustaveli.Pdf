# Rustaveli.Pdf

A free, open source, fluent PDF generation library for .NET.

> **Status: pre-release (0.x).** The layout engine, the composing API in its own print vocabulary and a managed PDF
> writer — font subsetting, pass-through images, CMYK and spot inks — are implemented and tested on `net10.0` and
> `netstandard2.0` (.NET Framework 4.6.2+), with page images through SkiaSharp. See [Roadmap](#roadmap).

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
      └─▶ Page images     Rustaveli.Pdf.Skia: PNG, JPEG or WebP through SkiaSharp
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
row boundaries), `List` (bulleted, numbered, lettered, roman — numbering survives new pages), `Layered`, `Banded`.

**Modifiers** — insets, fills, strokes, rounded corners, width and height constraints, expansion, proportion,
flush and centred placement, shifting, scaling, shrink-to-fit, quarter turns, mirroring.

**Text** — styled runs, weight, italic, ink, highlight, underline, strike-through, leading, tracking,
subscript and superscript, alignment, line breaking, mid-word breaking, non-breaking spaces, first-line indent,
space between paragraphs, flow across pages, folios, page counts and cross-references, and default type inherited
from the section.

**Flow** — `When`, `Once`, `SkipFirst`, `KeepTogether`, `RequireSpace`, `NewPage`, and reusable `ISnippet`s.

**Ink** — RGB, CMYK process colour and named spot inks with a process fallback, tints and opacity
([ADR 0004](docs/adr/0004-ink-colour-model.md)). There is no built-in palette: a document brings its own colours.

**Output** — PDF written in managed code: TrueType faces subset to the glyphs used and CFF faces embedded whole,
each searchable through a ToUnicode map; JPEGs and most PNGs embedded as they were encoded, with palettes, alpha,
colour keys, sixteen bits and ICC profiles kept, images shared by content and turned upright by their EXIF
orientation; CMYK process colour, spot inks as separations with a process fallback, and opacity; links,
cross-references to anchors and document information. Exports run in parallel. Page images — PNG, JPEG or WebP at
any resolution — come from the `Rustaveli.Pdf.Skia` package, drawn from the same layout and glyphs.

**Typefaces** — a `TypefaceLibrary` of registered and installed typefaces, matched by weight and slant, with named
fallbacks and per-character fallback for anything a face lacks, pair kerning, substitution for a typeface nobody
has, and bundled Noto Sans for a machine with no fonts at all.

## Testing

```bash
dotnet run eng/tools.cs       # once, on Windows: fetches the pinned, checksum-verified qpdf (elsewhere: apt/brew install qpdf)
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
renderers that share no code agree within half a percent of pixels.

**Benchmarks** (`benchmarks/Rustaveli.Pdf.Benchmarks`) measure throughput, allocations, parallel scaling and file
size against QuestPDF on a fixed set of documents, against the targets in
[ADR 0009](docs/adr/0009-performance-targets.md). File sizes on those documents:

| Document | QuestPDF | This library | Ratio |
|---|---:|---:|---:|
| Invoice | 15,011 B | 11,451 B | 0.76× |
| Report | 993,479 B | 671,147 B | 0.68× |
| Large table | 1,063,132 B | 837,225 B | 0.79× |
| Images | 3,581,499 B | 147,181 B | 0.04× |

Current agreement across text flow, header/footer pagination and multi-page tables: **identical page counts,
identical word sequences, identical horizontal word positions, and vertical positions within 0.24pt** — the
difference in where a reader boxes each word, from the fonts' declared descent, not in where the baselines fall.

Byte-level comparison is not meaningful — two PDF producers never emit identical bytes for the same document —
so the comparison is behavioural throughout.

> **On the QuestPDF test dependency.** The oracle is pinned to **2026.5.0**, the last release distributed under
> an MIT grant. Releases from 2026.6.0 onwards carry a licence whose restrictions forbid using the software to
> develop a competing PDF library, which would cover this test suite. Do not upgrade this reference.

## Known limitations

**A document is exported from one thread at a time.** Documents export in parallel, each with its own writer, but a
single `Document` instance carries the layout cursors in its block tree, so it must not be exported from two
threads at once. Compose one document per thread, or export them one after another.

**Placement expands to fill.** `Middle` and `FlushBottom` claim the whole height offered, and any placement claims
the whole width. Inside a running head or foot that means claiming the rest of the page; inside a `Natural` column
it defeats the point of natural sizing and starves the neighbouring columns. Both cases fail with an explanatory
error rather than producing silent garbage, but the general fix — resolving natural size before placing within it —
is not implemented. Constrain the size explicitly when placing.

**Text is set glyph by glyph, with pair kerning but no other OpenType features.** There are no ligatures or other
`GSUB` substitutions, no mark positioning, no complex-script shaping and no bidi reordering yet — the text engine of
phase 3. `ReadingDirection` mirrors *layout* — the order of columns and table columns, and the default text
alignment — but does not reorder characters within a string. Arabic, Hebrew and Indic text will not render
correctly. CJK will render but without proper line-breaking rules.

## Roadmap

The goal is every capability of QuestPDF — including its tooling — in a vocabulary of our own, and faster. The
[parity checklist](docs/parity/PARITY.md) tracks each capability against the phase that delivers it, and the
[architecture decision records](docs/adr/README.md) explain the choices behind the plan.

| Phase | Delivers |
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
| 9 | Documentation, and 1.0 |

## Licence

MIT. See [LICENSE](LICENSE).

The core package carries Latin, Greek and Cyrillic subsets of [Noto Sans](https://notofonts.github.io/), set only when
nothing registered or installed can set a document's text. They are distributed under the SIL Open Font License
1.1, which travels with them in the package (`licenses/NotoSans-OFL.txt`) and is in
[`src/Rustaveli.Pdf/Fonts/Bundled`](src/Rustaveli.Pdf/Fonts/Bundled/OFL.txt).

This project is independent and is not affiliated with, endorsed by, or sponsored by QuestPDF.
