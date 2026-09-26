# 0005 — Target net10.0 and netstandard2.0

**Status:** Accepted

## Context
The library targeted net8.0 and net10.0. .NET 8 leaves support in November 2026. A large share of the market for
PDF generation is enterprise code still on .NET Framework, which only a netstandard2.0 build can serve.

## Decision
Ship `net10.0` (spans, SIMD, trimming, AOT) and `netstandard2.0` (.NET Framework 4.6.2+, Mono, Unity). APIs missing
on netstandard2.0 come from Polyfill, a source-only package that compiles internal copies into the assembly, so the
shipped packages keep zero runtime dependencies. Every test runs on both `net10.0` and `net48`.

## Consequences
- One codebase serves modern and legacy runtimes.
- Hot paths may need `#if` branches where the modern API is materially faster.
- Default interface members and other runtime-dependent features are off the table in shipped code.
