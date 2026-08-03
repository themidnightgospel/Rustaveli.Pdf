# Conventions

Standing decisions about how code in this repository is written. Each entry records the rule, the reasoning,
and how it is enforced. Rules that a machine can check are configured in [`.editorconfig`](../.editorconfig) and
fail the build; the rest are here so they are at least written down rather than remembered.

New remarks get appended here, not just applied to the file that prompted them.

---

## Explicit types, never `var`

Write the type out:

```csharp
// Yes
SpacePlan plan = element.Measure(availableSpace, context);
List<TextLine> lines = BuildLines(width, height, context, out string? blocker);

// No
var plan = element.Measure(availableSpace, context);
```

**Why.** This codebase is mostly layout arithmetic, where the distinction between `float`, `Size`, `Position`
and `SpacePlan` is the whole substance of the code. `var` hides exactly the information a reader needs to check
that arithmetic — a review of a measurement routine should not require hovering over identifiers to learn what
is being measured. It also makes an accidental type change at a call site invisible at the point of use.

**Enforced.** `csharp_style_var_*` are all `false:error`, plus `dotnet_diagnostic.IDE0008.severity = error`.
With `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors` already on, a `var` fails the build.

**Exception.** Anonymous types cannot be named, so `var` is unavoidable there and the analyzer permits it.

---

## One top-level type per file, named after the type

`SpacePlan.cs` contains `SpacePlan` and nothing else. A file holding `FlipElement`, `EnsureSpaceElement` and
`ScaleToFitElement` gets split into three.

**Why.** Grouping several types under a plural filename (`SizingElements.cs`, `TransformElements.cs`) means the
only way to find a type is full-text search, and it lets a file grow without anyone noticing — the two largest
files in this repository are also the ones that have produced the most defects. One type per file makes the
file tree an index, keeps diffs attributable to a single type, and puts natural back-pressure on classes that
are quietly accumulating responsibilities.

**Nested types are not affected.** A private nested helper such as `TextElement.TextLine` belongs with its
parent; the rule is about *top-level* declarations.

**Enforced.** StyleCop `SA1402` (one type per file) and `SA1649` (file name matches the type), both `error`.
StyleCop.Analyzers is referenced build-time-only via `PrivateAssets="all"` and does not ship in any package.
Every other StyleCop rule is explicitly disabled — this repository is not adopting StyleCop's house style, only
borrowing its two structural checks, because plain `.editorconfig` cannot express them.

---

## Comments explain why, not what

Existing comments in this codebase are load-bearing: they record the reasoning behind a non-obvious choice,
usually one that a previous bug proved necessary. Keep that shape. A comment restating the code is noise; a
comment explaining why a wrapping is pinned, or why a lock exists, is the reason the next person does not
reintroduce the bug.
