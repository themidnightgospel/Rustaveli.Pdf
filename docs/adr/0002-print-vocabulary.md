# 0002 — A print and typesetting vocabulary, owned by a glossary

**Status:** Accepted

## Context
The first public API borrowed most of its names from the library that inspired its design
([Rustaveli.Pdf and QuestPDF](../questpdf.md)), internals included. This library is its own design; a borrowed
vocabulary makes it read as a copy and imports someone else's mental model along with the words.

## Decision
Names come from the vocabulary of print and typesetting, at the level a user of InDesign, Word or LaTeX would
recognise: *frame, rule, gutter, inset, leading, tracking, running head, folio, keep together, keep with next,
widows, orphans, bleed, trim*. Letterpress-era jargon that few readers know (*forme, galley, quoin, chase*) is
avoided. Where print has no term for a concept — z-order, conditional content, flexible sizing — the name is plain
descriptive English (`Above`, `When`, `Share`).

The rename covers everything, public API and internals alike, so the codebase speaks one language. It happens as
its own phase, before new features, so later work adds names in the new vocabulary rather than adding to what must
be renamed.

`docs/GLOSSARY.md` owns the vocabulary: every public type and member, its definition, and the print term it comes
from. A test fails when a public type is missing from it. New names enter the glossary in the same change that
introduces them.

## Consequences
- The API reads as a typesetting tool, which is what it is.
- Developers migrating from another library must learn new names; a migration guide maps them.
- Every naming discussion has a rule to settle it: *what would a typesetter call it?*
