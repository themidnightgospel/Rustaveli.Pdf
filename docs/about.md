# About the project

## Why this exists

Rustaveli.Pdf exists to give .NET a PDF tool that is:

- **Free** — MIT-licensed, for any project, with no revenue limits, keys or paid tiers.
- **Open** — every line of source public, to read, change and ship.
- **Complete** — from a one-page invoice to a PDF/A archive or an accessible, tagged document, so you never need a
  second library.
- **Dependable** — tested against independent validators, and released with signed provenance.

It is a design of its own:

- **A vocabulary drawn from print and typesetting** — trims, margins, running heads, folios, inks
  ([glossary](GLOSSARY.md)) — so the API reads like the documents it makes.
- **Plain .NET at the core.** The PDF writer, fonts, images and text engine are managed code with no native
  dependencies; native libraries come in only through the optional packages that need them.
- **Layout kept apart from output.** The layout engine draws through a single surface, so producing a PDF, a page
  image or anything else is a decision at the edge rather than one built into the core
  ([how layout works](how-it-works/layout.md)).


## History

The first release was built in ten phases, each merged only once it met every quality gate. The
[parity checklist](parity/PARITY.md) tracks each capability against the phase that delivered it, and the
[architecture decision records](adr/README.md) explain the choices behind the plan. Every phase below is done.

| Phase | Delivered |
|---|---|
| 0 | Groundwork: quality gates, pipelines, conformance and property tests, benchmarks |
| 1 | The print and typesetting vocabulary ([ADR 0002](adr/0002-print-vocabulary.md)); `Ink` colour |
| 2 | A managed PDF writer with font subsetting and parallel rendering |
| 3 | The text engine: line breaking, justification, fallback, OpenType features, complex scripts |
| 4 | Layout and styling parity, and named style sheets |
| 5 | Images and SVG |
| 6 | Output formats and conformance: page images, CMYK and spot colour, PDF/A, PDF/UA |
| 7 | Document operations: merge, overlay, attachments, encryption |
| 8 | A live preview tool with hot reload |
| 9 | Documentation, and the first release |


## Inspiration

The idea of describing a document as a fluent tree of content that breaks into pages by itself comes from QuestPDF.
How the two libraries relate — that inspiration, what QuestPDF is used for in this repository, and the clean-room
rewrite of this library's earliest code — is set out in [Rustaveli.Pdf and QuestPDF](questpdf.md).
