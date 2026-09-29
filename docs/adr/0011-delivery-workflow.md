# 0011 — Delivery in phases, one pull request each

**Status:** Accepted

## Context
The road to feature parity is long. It needs checkpoints where the whole system is known to be good.

## Decision
Work proceeds in phases, each on a `phase/NN-name` branch made of small commits that each build and pass. A phase
ends in one pull request. Two pipelines validate it:

- `ci.yml`, on every push: Release build with warnings as errors, both suites with the coverage gate on Linux, and
  the net10.0 and net48 legs on Windows.
- `pre-merge.yml`, once the pull request is labelled `ready-to-merge`: benchmarks and macOS.

Mutation testing runs apart from both, only at night on `main` (`nightly-mutation.yml`); see
[0007](0007-quality-gates.md).

A phase merges only when both are green. Phases, in order: groundwork; vocabulary; managed writer; text engine;
layout and styling parity; images and SVG; output formats and conformance; document operations; preview tool;
documentation and the first release.

## Consequences
- `main` is always releasable.
- A phase cannot quietly lower the bar: its gates are the same as every other phase's.
