# Rustaveli.Pdf

A free, open source, fluent PDF generation library for .NET.

> **Status: pre-release (0.x).** The layout engine, the composing API in its own print vocabulary and a SkiaSharp
> rendering backend are implemented and tested on `net10.0` and `netstandard2.0` (.NET Framework 4.6.2+). A managed
> PDF writer is about to replace Skia for PDF output — see [Roadmap](#roadmap).

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
ISurface              The single seam to any backend
      │
      ▼
Backend               Rustaveli.Pdf.Skia → SkiaSharp → PDF
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

**Output** — images with four fitting modes, links, cross-references to anchors, document information, PDF/A-2b
prerequisites via Skia.

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
change is approved with `dotnet run eng/approve-snapshots.cs` after inspecting the received and diff images.

**Benchmarks** (`benchmarks/Rustaveli.Pdf.Benchmarks`) measure throughput, allocations, parallel scaling and file
size against QuestPDF on a fixed set of documents, against the targets in
[ADR 0009](docs/adr/0009-performance-targets.md).

Current agreement across text flow, header/footer pagination and multi-page tables: **identical page counts,
identical word sequences, vertical positions within 0.04pt and horizontal within 3.3pt.**

Byte-level comparison is not meaningful — two PDF producers never emit identical bytes for the same document —
so the comparison is behavioural throughout.

> **On the QuestPDF test dependency.** The oracle is pinned to **2026.5.0**, the last release distributed under
> an MIT grant. Releases from 2026.6.0 onwards carry a licence whose restrictions forbid using the software to
> develop a competing PDF library, which would cover this test suite. Do not upgrade this reference.

## Known limitations

**Output files are large, because fonts are not subset.** Stock SkiaSharp's PDF backend embeds each typeface in
full rather than emitting only the glyphs a document actually uses. Measured on the equivalence recipes, where
the reader recovers byte-identical content from both files:

| Recipe | This library | QuestPDF |
|---|---|---|
| Text flow | 570,999 B | 32,116 B |
| Header/footer pagination | 573,997 B | 31,491 B |
| Table | 574,133 B | 32,182 B |

QuestPDF avoids this by shipping its own native Skia build with the HarfBuzz subsetter wired in; the published
SkiaSharp package exposes no equivalent. This library closes the gap by writing PDF itself, with font subsetting
in managed code ([ADR 0001](docs/adr/0001-managed-pdf-writer.md)). The `DivergenceReportTests` print these sizes
on every run so the number stays visible.

**Rendering is serialised.** Skia's PDF backend keeps process-wide font state that concurrent renders corrupt:
the resulting file is structurally valid and roughly the right size, but its embedded font encoding no longer
matches its text operators, so every glyph extracts as U+0000 — and nothing throws. This was reproduced with a
separate font provider, document and output stream per thread, which leaves Skia's own caches as the only shared
state. Export therefore takes a process-wide lock. `PdfExportOptions.AllowConcurrentRendering` opts out if you
have measured your own workload.

Relatedly, a `SkiaFontProvider` must be long-lived. One created per document and dropped will, once collected,
release typefaces that other renders are still using. Use `SkiaFontProvider.Shared` unless you have a reason not
to. And a single `Document` instance must not be exported from two threads at once — it carries the layout
cursors in its block tree.

**Placement expands to fill.** `Middle` and `FlushBottom` claim the whole height offered, and any placement claims
the whole width. Inside a running head or foot that means claiming the rest of the page; inside a `Natural` column
it defeats the point of natural sizing and starves the neighbouring columns. Both cases fail with an explanatory
error rather than producing silent garbage, but the general fix — resolving natural size before placing within it —
is not implemented. Constrain the size explicitly when placing.

**Text shaping is advance-width only.** There is no complex-script shaping, no font fallback chain and no bidi
reordering. `ReadingDirection` mirrors *layout* — the order of columns and table columns, and the default text
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

This project is independent and is not affiliated with, endorsed by, or sponsored by QuestPDF.
