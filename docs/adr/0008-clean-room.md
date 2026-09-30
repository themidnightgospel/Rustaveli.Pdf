# 0008 — Another library is a reference for behaviour, never a source

**Status:** Accepted

## Context
This library offers the capabilities of an established library whose design inspired it, and checks itself against
that library ([Rustaveli.Pdf and QuestPDF](../questpdf.md)). Whatever the licence of the other library's code, this
one is meant to be a design of its own: the value of a fresh design is lost if another library's structure seeps in,
and code written here is code its maintainers fully understand.

## Decision
Another library is used only for its behaviour:

- its public API surface, listed by reflection, to build the feature-parity checklist;
- its documentation, to understand what a feature does;
- its output, as the oracle in equivalence tests and the baseline in benchmarks.

Nobody writing this library's code reads the other library's source, and nothing is ported from it. When its source
must be compared with ours — to audit this library — the work is split as in a clean room: reviewers who read it
write specifications of behaviour in prose, and developers who have not read it write the code.

A reference is pinned to a release whose licence permits this use, and is not upgraded past it.

## Consequences
- Designs are our own. Hard problems (table spanning, line breaking) are solved independently, which costs time but
  yields code we fully understand.
- An audit in September 2026 found that parts of the earliest code had not kept to this rule. They were rewritten in
  a clean room; [Rustaveli.Pdf and QuestPDF](../questpdf.md) records what was found and what was done.
