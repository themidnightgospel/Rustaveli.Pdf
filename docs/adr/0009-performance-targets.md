# 0009 — Performance targets, measured against QuestPDF

**Status:** Accepted

## Context
"Faster" means nothing until it is measured. QuestPDF runs on native Skia and is the obvious yardstick.

## Decision
A BenchmarkDotNet suite renders a fixed set of documents — a one-page invoice, a 100-page report, a 10 000-row
table, text-heavy and image-heavy documents — through both libraries. Targets, against QuestPDF 2026.5.0:

| Metric | Target |
|---|---|
| Throughput, single thread | ≥ 1.5× pages per second |
| Allocations per page | ≤ 0.5× |
| Eight documents in parallel | ≥ 6× single-document throughput |
| File size | ≤ 1.1× |
| Time to first page | ≤ 1.0× |

A pull request that regresses any metric by more than 10% against the baseline recorded on main fails. The targets
apply from the phase that lands the managed writer; before then the Skia backend's global lock makes the parallel
target unreachable by construction.

## Consequences
- Performance becomes a tested property, not a claim.
- Benchmarks run only in the pre-merge checks, once a pull request is ready to merge, where their noise and cost
  are acceptable.
