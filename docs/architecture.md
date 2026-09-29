# How layout works

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

## The fitting contract

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
allots each child its final size ([ADR 0012](adr/0012-parents-allot-final-size.md)). `Plan` must be free of
side effects, because the typesetter plans speculatively and discards results.

## Counting passes

Content such as "Page 3 of 12" cannot resolve in a single pass, because the page count is unknown until
pagination finishes. The typesetter therefore sets the document more than once — first to a page sink that
discards everything, purely to count pages, repeating until the count settles, then for real. Every pass runs
identical layout code, so the count cannot drift.

## Per-page state

Running heads, running feet and repeating table rows are set in full on every page, but their *content* tracks how
much of itself it has set. These get a softer reset between pages that clears pagination progress while preserving
document-wide state, so a `Once` frame inside a running head still appears only once. Getting this wrong is what
made table header rows silently vanish after page one — a defect the QuestPDF comparison suite caught.
