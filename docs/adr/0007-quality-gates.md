# 0007 — Quality gates: coverage, mutation, validators, snapshots

**Status:** Accepted

## Context
A PDF writer fails in ways a reader-based integration test cannot see: a file that parses but violates the spec, a
border drawn in the wrong place, a subset font missing a glyph. And coverage numbers alone reward tests that execute
code without checking anything.

## Decision
Every phase must pass, before merge:

- **Coverage** of the shipped assemblies, both suites combined: ≥ 95% line, ≥ 90% branch, never decreasing.
  Enforced by `dotnet run eng/coverage.cs`. Exclusions need `[ExcludeFromCodeCoverage(Justification = ...)]`.
- **Mutation testing** (Stryker.NET): score ≥ 80% in every shard, proving the tests detect wrong behaviour. Every
  night, and only then, the whole engine is mutated on `main`, and a shard below the floor opens an issue for the
  next pull request to fix. Pull requests are not mutation-tested: even mutating only the files a pull request
  changes held merges for hours. Arguments to exception constructors — the words of an error message — are not
  mutated.
- **Unit tests** against a deterministic fake measurer and a recording canvas, so expected values are computable by
  hand.
- **Integration tests** reading real output back with PdfPig.
- **Conformance validators**: qpdf `--check` on generated files; veraPDF for PDF/A and PDF/UA output.
- **Visual regression**: pages rendered by an independent renderer (PDFium) and compared with approved snapshots.
- **Property-based tests and fuzzing** (CsCheck): random element trees must paginate, terminate and produce valid
  output; font parsing survives malformed input.
- **Equivalence** with the QuestPDF oracle for features both libraries share.

## Consequences
- Slower phases, far fewer regressions. The expensive gates (benchmarks, macOS) run once per pull request when it
  is ready to merge rather than on every push. See [0011](0011-delivery-workflow.md).
- A change that weakens the tests passes its own gate and is caught by the next nightly run — a day's delay,
  accepted for merges that do not wait hours.
