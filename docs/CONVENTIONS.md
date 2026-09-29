# Conventions

Standing decisions about how code in this repository is written. Each entry records the rule, the reasoning,
and how it is enforced. Rules that a machine can check are configured in [`.editorconfig`](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/.editorconfig) and
fail the build; the rest are here so they are at least written down rather than remembered.

New remarks get appended here, not just applied to the file that prompted them.

---

## Explicit types, never `var`

Write the type out:

```csharp
// Yes
Fit plan = block.Plan(availableSpace, context);
List<TextLine> lines = BuildLines(width, height, context, out string? blocker);

// No
var plan = block.Plan(availableSpace, context);
```

**Why.** This codebase is mostly layout arithmetic, where the distinction between `float`, `Extent`, `Offset`
and `Fit` is the whole substance of the code. `var` hides exactly the information a reader needs to check
that arithmetic — a review of a measurement routine should not require hovering over identifiers to learn what
is being measured. It also makes an accidental type change at a call site invisible at the point of use.

**Enforced.** `csharp_style_var_*` are all `false:error`, plus `dotnet_diagnostic.IDE0008.severity = error`.
With `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors` already on, a `var` fails the build.

**Exception.** Anonymous types cannot be named, so `var` is unavoidable there and the analyzer permits it.

---

## One top-level type per file, named after the type

`Fit.cs` contains `Fit` and nothing else. A file holding `MirrorBlock`, `RequireSpaceBlock` and
`ShrinkToFitBlock` gets split into three.

**Why.** Grouping several types under a plural filename (`SizingBlocks.cs`, `TransformBlocks.cs`) means the
only way to find a type is full-text search, and it lets a file grow without anyone noticing — the two largest
files in this repository are also the ones that have produced the most defects. One type per file makes the
file tree an index, keeps diffs attributable to a single type, and puts natural back-pressure on classes that
are quietly accumulating responsibilities.

**Nested types are not affected.** A private nested helper such as `TextBlock.TextLine` belongs with its
parent; the rule is about *top-level* declarations.

**Enforced.** StyleCop `SA1402` (one type per file) and `SA1649` (file name matches the type), both `error`.
StyleCop.Analyzers is referenced build-time-only via `PrivateAssets="all"` and does not ship in any package.
Every other StyleCop rule is explicitly disabled — this repository is not adopting StyleCop's house style, only
borrowing its two structural checks, because plain `.editorconfig` cannot express them.

---

## Warnings are errors in Release

`TreatWarningsAsErrors` is on for `Release` only. A Debug build reports warnings and keeps going; a Release
build fails.

**Why.** Release is what ships and what CI gates on, so nothing should leave the repository with a warning
outstanding. Applying the same rule to Debug makes the loop worse rather than better: an unused variable in a
half-written method stops the build, which stops the test suite, which is exactly when the tests are most
worth running.

This does **not** relax the rules configured as `error` in `.editorconfig` — explicit types (`IDE0008`) and
one-type-per-file (`SA1402`, `SA1649`) fail in every configuration. Those are structural decisions about how
the code is written, not advisory diagnostics, so a Debug build gets no discount on them.

**Enforced.** `Directory.Build.props`, conditioned on `'$(Configuration)' == 'Release'`.

---

## No unused `using` directives

An import that nothing in the file needs is removed.

**Why.** The import block is the cheapest available summary of what a file depends on. Once it lists namespaces
the file stopped using three edits ago, it stops being that summary and starts actively misleading — a reader
checking whether the layout engine has crept into a dependency on the rendering backend cannot trust it. Stale
imports also mask a real one: a `using` left over from deleted code looks identical to a `using` that is load-
bearing.

**Warning, not error.** Unlike explicit types and one-type-per-file, this is tidiness rather than structure. A
half-finished edit routinely leaves an import briefly orphaned, and stopping the build for it would stop the
test run too. Debug reports it; the Release gate turns it into a failure, so nothing ships with one.

**Enforced.** `dotnet_diagnostic.IDE0005.severity = warning`.

There is a trap worth knowing: IDE0005 is reported by a command-line build **only** when the project generates
a documentation file. Without `GenerateDocumentationFile`, the compiler skips the analysis the rule reads from
and the warning appears in the IDE alone — green in CI, red on a developer's screen. `Directory.Build.props`
therefore switches doc generation on for every project, and suppresses `CS1591` alongside it: the objective is
unused imports, not a doc comment on every member.

To clear them in bulk:

```
dotnet format style --diagnostics IDE0005 --severity warn
```

Set the two `src` projects to a single `TargetFrameworks` value while doing so. `dotnet format` computes its fix
once per target framework and, on a multi-targeted project, writes both into the file as unresolved merge
conflict markers — it has corrupted source in this repository before.

---

## Every change is verified by a build and a test run

A change is not finished when it is written. Build the solution and run the full suite, and check Release when
the question is warnings:

```
dotnet build -c Release
dotnet test
```

**Why.** This repository lost 23 types to a scripted bulk edit whose damage was invisible until the next build
was attempted. Verifying continuously bounds how far a bad change travels. It also keeps the test suite honest
as a description of current behaviour rather than of behaviour from several edits ago.

---

## Comments explain why, not what

Existing comments in this codebase are load-bearing: they record the reasoning behind a non-obvious choice,
usually one that a previous bug proved necessary. Keep that shape. A comment restating the code is noise; a
comment explaining why a wrapping is pinned, or why a lock exists, is the reason the next person does not
reintroduce the bug.

---

## LF line endings everywhere

Every text file is stored and checked out with LF, on every operating system. Windows `.cmd`/`.bat` scripts are
the only exception, because `cmd.exe` requires CRLF.

**Why.** CI builds on Linux, Windows and macOS, and tests compare generated text and images against approved
files. With `core.autocrlf` left to each machine, the same commit produces different bytes on different
checkouts, and a file written by a tool arrives with whichever ending that tool prefers — this repository had
both mixed in before the rule existed. One canonical form removes the whole class of "passes here, fails there"
differences. Every editor and IDE in use on Windows handles LF without complaint.

**Enforced.** `.gitattributes` (`* text=auto eol=lf`) normalises on commit and checkout regardless of local git
settings; `.editorconfig` (`end_of_line = lf`) makes editors write LF in the first place.

---

## Public types live in one namespace; everything else is internal

Every public type is in the `Rustaveli.Pdf` namespace, whichever package or folder it is in, so one `using`
directive is enough to write any document. `Composition/`, `Primitives/` and `Exceptions/` hold public types only.
A topic folder may hold a public type beside its internals — `TypefaceLibrary` in `Fonts/`, `RasterImage` in
`Images/`, `PdfExport` in `Output/` — and its internal types take a namespace named after the folder:
`Rustaveli.Pdf.Fonts`, `Rustaveli.Pdf.Output`. `Blocks/`, `Layout/`, `Drawing/`, `Text/` and `Writing/` are
internal throughout.

Nothing public may expose an internal type. A composer reaches its block through an internal constructor, and
`IFrame` hides the block it holds behind the internal `IFrameSlot`. Tests reach internals through
`InternalsVisibleTo`; a public test method that needs an internal enum takes its name as a string.

**Why.** A small public surface is what lets the engine change: blocks, the typesetter and the drawing seam are
rewritten phase by phase, and none of that should break a caller. One namespace also keeps the vocabulary in one
place, where [the glossary](GLOSSARY.md) can account for every word of it.

**Enforced.** `GlossaryTests` fails when a public type is outside `Rustaveli.Pdf` or missing from the glossary,
and the compiler rejects a public signature that mentions an internal type (`CS0051`).
