# Rustaveli.Pdf

A free, open source, fluent PDF generation library for .NET.

> **Status: pre-release (0.x).** The layout engine, element model, fluent API and a SkiaSharp rendering backend
> are implemented and tested on `net10.0` and `netstandard2.0` (.NET Framework 4.6.2+). The public vocabulary is
> about to change and a managed PDF writer is about to replace Skia for PDF output — see [Roadmap](#roadmap).

## Why this exists

QuestPDF popularised the fluent, composable approach to building PDFs in .NET. As of release **2026.6.0**
(June 2026) it moved from an MIT grant to a source-available commercial licence that is explicitly not
OSI-approved, excludes public-sector bodies and publicly traded companies from its free tier regardless of
revenue, and gates everyone else behind a $1M annual revenue threshold.

This project provides the same category of tool under a genuinely permissive licence, with no revenue gate and
no eligibility classes.

It is written from scratch rather than forked. [FossPDF](https://github.com/lol768/FossPDF.NET) already
continues QuestPDF's MIT-era code and is the shorter path to a free library; this codebase exists to be a fresh
design, with the layout engine kept strictly behind an `ICanvas` seam so the rendering backend remains a
replaceable decision rather than a foundational one.

## Quick start

```csharp
using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;

var document = Document.Create(container => container.Page(page =>
{
    page.Size = PageSizes.A4;
    page.Margin = Edges.All(40);
    page.DefaultTextStyle = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(11);

    page.Header().Text("Quarterly Statement");

    page.Footer().Text(text =>
    {
        text.Span("Page ");
        text.CurrentPageNumber();
        text.Span(" of ");
        text.TotalPages();
    });

    page.Content().Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(90);
            columns.RelativeColumn();
            columns.ConstantColumn(70);
        });

        table.Header(header =>
        {
            header.Cell().Text("Code");
            header.Cell().Text("Description");
            header.Cell().Text("Amount");
        });

        foreach (var row in rows)
        {
            table.Cell().Text(row.Code);
            table.Cell().Text(row.Description);
            table.Cell().Text(row.Amount);
        }
    });
}));

document.GeneratePdf("statement.pdf");
```

## Architecture

The library is split so that layout never depends on how pixels or PDF operators are produced.

```
Fluent API            IContainer extension methods; descriptors for text, rows, columns, tables
      │
      ▼
Composition tree      Element graph — containers, decorators, leaf content
      │
      ▼
Layout engine         Measure(availableSpace) → SpacePlan, then Draw(availableSpace, canvas)
                      Pagination orchestrator; two-pass rendering for page totals
      │
      ▼
ICanvas               The single seam to any backend
      │
      ▼
Backend               Rustaveli.Pdf.Skia → SkiaSharp → PDF
```

### The measurement contract

Every element answers one question before anything is drawn: *given this much space, what would you do?* The
answer is a `SpacePlan` with four outcomes.

| Outcome | Meaning |
|---|---|
| `Empty` | Nothing left to draw. A continuation page must not give it space again. |
| `Wrap` | Cannot be drawn at all here — defer to the next page. |
| `PartialRender` | Drew what fits; the remainder continues on the next page. |
| `FullRender` | Drew everything. |

Pagination falls out of this. The engine responds to `Wrap` by starting a fresh page and retrying — but if an
element wraps on a page that is *already empty*, no further page could ever help, so it raises
`DocumentLayoutException` rather than looping forever.

Elements must honour one rule: **`Draw` may only consume the space `Measure` promised for the same input.**
`Measure` must be free of side effects, because the engine measures speculatively and discards results.

### Two-pass rendering

Content such as "Page 3 of 12" cannot resolve in a single pass, because the total is unknown until pagination
finishes. The generator therefore renders the document twice — first to a canvas that discards everything,
purely to count pages, then for real. Both passes run identical layout code, so the count cannot drift.

### Per-page state

Headers, footers and repeating table bands are drawn in full on every page, but their *content* tracks how much
of itself it has drawn. These get a softer reset between pages that clears pagination progress while preserving
document-wide state, so a `ShowOnce` marker inside a header still appears only once. Getting this wrong is what
made table headers silently vanish after page one — a defect the QuestPDF comparison suite caught.

## What's implemented

**Layout** — page slots (header, content, footer, background, foreground), fixed and continuous page sizes,
margins, `Row` (constant/relative/auto items, RTL), `Column`, `Table` (relative and constant columns, automatic
and explicit cell placement, row and column spanning, repeating header/footer bands, pagination at row
boundaries), `List` (bulleted, numbered, lettered, roman — numbering survives page breaks), `Layers`,
`Decoration`.

**Decorators** — padding, background, border, rounded corners, width/height constraints, min/max, extend,
aspect ratio, alignment, translate, scale, scale-to-fit, quarter-turn rotation, horizontal/vertical flip.

**Text** — styled spans, weight, italic, colour, background highlight, underline, strikethrough, line height,
letter spacing, subscript/superscript, alignment, word wrap, mid-word breaking, non-breaking spaces, first-line
indent, paragraph spacing, page-break flow, dynamic page numbers and section references, style inheritance from
page defaults.

**Flow control** — `ShowIf`, `ShowOnce`, `SkipOnce`, `ShowEntire`, `EnsureSpace`, `PageBreak`, reusable
`IComponent`s.

**Colour** — the full Material Design palette: 19 families × 10 shades, plus 4 accents on the 16 chromatic ones.
Each family converts implicitly to its base shade, so `Colors.Red` and `Colors.Red.Lighten3` both read naturally.

**Output** — images with four fit modes, external hyperlinks, internal links and named destinations, document
metadata, PDF/A-2b prerequisites via Skia.

## Testing

```bash
dotnet run eng/tools.cs       # once: fetches the pinned, checksum-verified qpdf the conformance tests use
dotnet test                   # every suite, on net10.0 and (on Windows) net48
dotnet run eng/coverage.cs    # both suites with coverage, enforcing 95% line / 90% branch
dotnet stryker                # mutation testing of the engine, failing below 80%
```

Every test runs against both builds the library ships: `net10.0`, and the `netstandard2.0` build on .NET Framework
4.8. The quality gates each pull request must pass are recorded in
[ADR 0007](docs/adr/0007-quality-gates.md).

**Unit tests** run the layout engine against a deterministic fake text measurer — every character is half the
font size wide, every line exactly the font size tall — and a recording canvas that resolves each drawing
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
state. Generation therefore takes a process-wide lock. `PdfGenerationOptions.AllowConcurrentRendering` opts out
if you have measured your own workload.

Relatedly, a `SkiaFontProvider` must be long-lived. One created per document and dropped will, once collected,
release typefaces that other renders are still using. Use `SkiaFontProvider.Shared` unless you have a reason not
to. And a single `Document` instance must not be rendered from two threads at once — it carries the layout
cursors in its element tree.

**Alignment expands to fill.** `AlignMiddle`/`AlignBottom` claim the whole height offered, and any alignment
claims the whole width. Inside a page header or footer that means claiming the rest of the page; inside an
`AutoItem` it defeats the point of Auto sizing and starves the neighbouring columns. Both cases now fail with an
explanatory error rather than producing silent garbage, but the general fix — resolving natural size before
aligning within it — is not implemented. Constrain the size explicitly when aligning.

**Text shaping is advance-width only.** There is no complex-script shaping, no font fallback chain and no bidi
reordering. `ContentDirection` mirrors *layout* — the order of row items and table columns, and the default text
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
