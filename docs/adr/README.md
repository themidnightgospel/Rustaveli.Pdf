# Architecture decision records

Each record captures one decision that shapes the library: the context it was made in, what was decided, and what
follows from it. Records are immutable once accepted — a changed mind is a new record that supersedes the old one,
so the history of *why* survives.

| # | Decision | Status |
|---|---|---|
| [0001](0001-managed-pdf-writer.md) | Write PDF ourselves; Skia only for raster output | Accepted |
| [0002](0002-print-vocabulary.md) | A print and typesetting vocabulary, owned by a glossary | Accepted |
| [0003](0003-api-shape-and-style-sheets.md) | Keep the fluent shape; add named style sheets | Accepted |
| [0004](0004-ink-colour-model.md) | Colour is Ink: RGB, CMYK and spot, with tints | Accepted |
| [0005](0005-target-frameworks.md) | Target net10.0 and netstandard2.0 | Accepted |
| [0006](0006-packages-and-dependencies.md) | A dependency-free core; native code only by opt-in | Accepted |
| [0007](0007-quality-gates.md) | Quality gates: coverage, mutation, validators, snapshots | Accepted |
| [0008](0008-clean-room.md) | QuestPDF is a behavioural reference, never a source | Accepted |
| [0009](0009-performance-targets.md) | Performance targets, measured against QuestPDF | Accepted |
| [0010](0010-preview-tooling.md) | Developer tooling: a dotnet tool with a browser UI | Accepted |
| [0011](0011-delivery-workflow.md) | Delivery in phases, one pull request each | Accepted |
| [0012](0012-parents-allot-final-size.md) | Parents allot the final size; decorators fill it | Accepted |
| [0013](0013-span-compatibility-package.md) | Spans on .NET Framework come from System.Memory | Accepted |
| [0014](0014-text-is-shaped-once.md) | Text is shaped once, in the core, for measuring and drawing alike | Accepted |

A new record has three sections: **Context** (the forces at play), **Decision** (what we will do) and
**Consequences** (what becomes easier, what becomes harder, what we now owe).
