# 0013 — Spans on .NET Framework come from System.Memory

**Status:** Accepted. Amends [0006](0006-packages-and-dependencies.md).

## Context
The managed PDF writer and the font and image parsers are byte-level code where `Span<T>`, `ReadOnlySpan<T>` and
`ArrayPool<T>` decide whether they allocate per glyph or not at all. .NET 10 provides these types in the runtime.
netstandard2.0 does not: on .NET Framework they come from Microsoft's `System.Memory` package, which cannot be
polyfilled from source because the runtime and other libraries must agree on the type identity.

The alternative — writing every parser twice, or against arrays and offsets only — would make the modern target
slower to match the old one, or double the code that most needs to be right.

## Decision
The core references `System.Memory` for its netstandard2.0 build only. The net10.0 build still has no dependencies
at all. No other runtime dependency is allowed under this exception.

## Consequences
- One implementation of every parser and writer, allocation-conscious on both targets.
- .NET Framework consumers receive `System.Memory` and its two small companions — packages already present in most
  .NET Framework applications through other libraries.
