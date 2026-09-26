# 0008 — QuestPDF is a behavioural reference, never a source

**Status:** Accepted

## Context
QuestPDF 2026.5.0 is the last release under an MIT grant, so reading or even adapting its source would be legally
permitted with attribution. But this library is written from scratch, and the value of a fresh design is lost if
another library's structure seeps in.

## Decision
QuestPDF is used only for its behaviour:

- its public API surface, enumerated by reflection, to build the feature-parity checklist;
- its documentation, to understand what a feature does;
- its rendered output, as the oracle in equivalence tests.

Its source is never read or ported. The test dependency stays pinned to 2026.5.0: later releases forbid use in
developing a competing library.

## Consequences
- "Written from scratch" stays true, and designs are our own.
- Hard problems (table spanning, line breaking) are solved independently, which costs time but yields code we fully
  understand.
