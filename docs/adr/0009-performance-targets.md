# 0009 — Performance targets, measured against a reference

**Status:** Accepted

## Context
"Faster" means nothing until it is measured. The library whose capabilities this one offers runs on native Skia and
is the obvious yardstick ([Rustaveli.Pdf and QuestPDF](../questpdf.md)).

## Decision
A BenchmarkDotNet suite renders a fixed set of documents — a one-page invoice, a 100-page report, a 10 000-row
table, text-heavy and image-heavy documents — through both libraries. Targets, against the pinned reference release:

| Metric | Target |
|---|---|
| Throughput, single thread | ≥ 1.5× pages per second |
| Allocations per page | ≤ 0.5× |
| Eight documents in parallel | ≥ 6× single-document throughput |
| File size | ≤ 1.1× |
| Time to first page | ≤ 1.0× |

A pull request whose allocations or file sizes grow by more than 10% against the baseline recorded on main fails.
Times are compared with main's too, and shown, but not held to it: hosted runners differ by 10–20% from one run to
the next, so a time measured on one is no baseline for a time measured on another. The targets apply from the phase
that lands the managed writer; before then the Skia backend's global lock makes the parallel target unreachable by
construction.

## Consequences
- Performance becomes a tested property, not a claim.
- Benchmarks run in the pre-merge checks, once a pull request is ready to merge, where their noise and cost are
  acceptable, and on main after every merge, which records the baseline. github-action-benchmark keeps main's
  numbers on the `benchmark-data` branch, and the documentation site charts their history; each pull request is
  told how its numbers compare with main's last.
