# About the project

## Background

QuestPDF popularised the fluent, composable approach to building PDFs in .NET. As of release **2026.6.0**
(June 2026) it moved from an MIT grant to a source-available commercial licence that is explicitly not
OSI-approved, excludes public-sector bodies and publicly traded companies from its free tier regardless of
revenue, and gates everyone else behind a $1M annual revenue threshold.

This project provides the same category of tool under a genuinely permissive licence, with no revenue gate and
no eligibility classes.

It is written from scratch rather than forked. [FossPDF](https://github.com/lol768/FossPDF.NET) already
continues QuestPDF's MIT-era code and is the shorter path to a free library; this codebase exists to be a fresh
design, in a vocabulary of its own drawn from print and typesetting ([glossary](GLOSSARY.md)), with the layout
engine kept strictly behind a drawing-surface seam so the output backend remains a replaceable decision rather than
a foundational one.


## History

The first release delivers every capability of QuestPDF — including its tooling — in a vocabulary of our own. The
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


## Independence

This project is independent and is not affiliated with, endorsed by, or sponsored by QuestPDF.
