# Rustaveli.Pdf and QuestPDF

[QuestPDF](https://www.questpdf.com/) is a well-known .NET library for generating PDF documents. This page is the one
place that sets out how Rustaveli.Pdf relates to it: what was taken as inspiration, what QuestPDF is used for in this
repository, what an audit of the code found, and what was done about it. The rest of the documentation describes
Rustaveli.Pdf on its own terms.

If you are moving a codebase across, [Coming from QuestPDF](guide/coming-from-questpdf.md) is the practical guide.

## Design inspiration

QuestPDF showed that a PDF document can be described in C# as a fluent tree: content placed in frames that hold one
thing each, modifiers that wrap what they are given, and layout that breaks the pages on its own. Rustaveli.Pdf was
inspired by that design, and sets out to offer every capability QuestPDF offers — the
[parity checklist](parity/PARITY.md) follows each one.

Ideas, features and the general shape of an API are not owned by any one library; the way they are expressed — the
code, its names and its documentation — is. Rustaveli.Pdf's expression is its own:

- **Its own vocabulary.** Names come from print and typesetting — trim, inset, folio, running head, overset — not
  from QuestPDF ([ADR 0002](adr/0002-print-vocabulary.md), [glossary](GLOSSARY.md)). A test fails if a public name
  coined by QuestPDF appears in this library; only plain words such as `Table` or `Width` may be shared.
- **Its own architecture.** A managed PDF writer with no native dependencies, fonts subset and text shaped in managed
  code, layout drawn through a single surface, options given per export rather than set globally.
- **Its own implementation**, written independently — see [the clean-room rewrite](#the-clean-room-rewrite) below.

## What QuestPDF is used for here

Apart from the audit described [below](#the-clean-room-rewrite), QuestPDF is used only as its published NuGet
package, and only in tests, benchmarks and tooling:

- **A test oracle.** Equivalence tests compose the same documents through both libraries and compare what a PDF
  reader recovers from each file ([testing](testing.md)).
- **A benchmark baseline** for speed, memory and file size ([performance](performance.md)).
- **The parity checklist.** QuestPDF's public API, listed by reflection over the package, is the raw material for the
  [parity checklist](parity/PARITY.md).

No Rustaveli.Pdf package depends on QuestPDF or ships any part of it.

The package is pinned to **2026.5.0**. That release is published under the QuestPDF License, which lets open-source
projects use it under its Community MIT terms. Releases from 2026.6.0 on carry a licence that forbids using them to
develop a competing library, so the pin is deliberate and is never upgraded; the dependency updater is told to leave
it alone.

## Comparisons

The equivalence suite composes the same text flow, running heads and feet, and multi-page tables through both
libraries and reads both files back. They agree on **page counts and word sequences**, and every word sits within
4 pt across and 0.5 pt down of where the other library puts it.

File sizes of the benchmark documents:

| Document | QuestPDF 2026.5.0 | Rustaveli.Pdf | Ratio |
|---|---:|---:|---:|
| Invoice | 15,011 B | 6,799 B | 0.45× |
| Report | 993,479 B | 171,231 B | 0.17× |
| Large table | 1,063,132 B | 581,890 B | 0.55× |
| Images | 3,581,499 B | 142,133 B | 0.04× |

Speed and memory are measured on every pull request that is ready to merge; see [performance](performance.md).

## The clean-room rewrite

From the start, the project's rule was that QuestPDF is a reference for behaviour, never a source of code
([ADR 0008](adr/0008-clean-room.md)).

In September 2026 an audit compared this library's code, line by line, with the source of three QuestPDF releases —
2021.12, 2022.12.15 and 2026.5.0. It found that the rule had not been kept everywhere. Parts of the earliest code
followed QuestPDF's implementation too closely: the core types of the layout engine (the answer a block gives about
the room it is offered, sizes and positions, the block with a single child), most of the layout blocks, the
interface blocks draw through, and some of the composers and their documentation. The rest of the library — the PDF
writer, fonts, text shaping, images, SVG, colour, encryption, tagging, existing-file operations, the preview and the
raster output — was found to be independent.

Everything the audit found was rewritten under a clean-room process:

1. **Audit.** Reviewers who read QuestPDF's source listed each part of this library that followed it, and wrote a
   specification of what that part must do — its behaviour, in prose, with no code and no structure taken from
   either library.
2. **Rewrite.** Developers who did not see QuestPDF's source, or the code being replaced, wrote each part anew from
   those specifications and the project's own tests. The code being replaced was removed before they began.
3. **Verification.** A second audit compared every rewritten part with all three QuestPDF releases: 45 parts, every
   one an independent expression, none still following QuestPDF's.
4. **Vocabulary.** Comments, names and tests across the rest of the codebase were brought into the library's own
   vocabulary.

The rewrite landed in [#55](https://github.com/themidnightgospel/Rustaveli.Pdf/pull/55) and
[#56](https://github.com/themidnightgospel/Rustaveli.Pdf/pull/56). Every test that held the library's behaviour before
it holds it after, and version 0.2.0 is the first release built entirely from the rewritten code.

## Licensing of the earlier code

Versions **0.1.0** and **0.1.1**, and the repository history before the rewrite, contain the earlier code.

QuestPDF 2021.12 and 2022.12.15 were released under the MIT License. QuestPDF 2026.5.0 was released under the QuestPDF
License, whose Community MIT terms apply to open-source projects such as this one. Both grant permission to use,
copy, modify, merge, publish and distribute the software, on one condition: that the copyright notice and the
permission notice are included. A licence QuestPDF adopted later applies to the releases published under it; it does
not change the terms of the releases before it, and this project uses none of the later ones.

The earlier releases of Rustaveli.Pdf did not include that notice, and they should have. It is given below, and in the
release notes of 0.1.0 and 0.1.1. Those two versions are unlisted on NuGet in favour of 0.2.0: existing projects that
reference them still restore, but they are no longer offered to new ones.

```text
MIT License

Copyright (c) 2021 QuestPDF

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Names and trademarks

QuestPDF is the name of its owners' product, used here only to refer to it. Rustaveli.Pdf is an independent project,
not affiliated with, endorsed by or sponsored by QuestPDF or its authors.

## Questions

If you have a question or a concern about anything on this page, please
[open an issue](https://github.com/themidnightgospel/Rustaveli.Pdf/issues).
