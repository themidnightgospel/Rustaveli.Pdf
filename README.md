<!-- github-only -->
<p align="center">
  <img src="https://raw.githubusercontent.com/themidnightgospel/Rustaveli.Pdf/main/docs/images/logo.png" alt="Rustaveli.Pdf" width="112">
</p>

<h1 align="center">Rustaveli.Pdf</h1>

<p align="center">Free and fully featured PDF generation for C# and .NET</p>

<p align="center">
  <a href="https://github.com/themidnightgospel/Rustaveli.Pdf/actions/workflows/ci.yml"><img src="https://github.com/themidnightgospel/Rustaveli.Pdf/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI"></a>
  <a href="https://github.com/themidnightgospel/Rustaveli.Pdf/actions/workflows/codeql.yml"><img src="https://github.com/themidnightgospel/Rustaveli.Pdf/actions/workflows/codeql.yml/badge.svg?branch=main" alt="CodeQL"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/themidnightgospel/Rustaveli.Pdf"><img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.scorecard.dev%2Fprojects%2Fgithub.com%2Fthemidnightgospel%2FRustaveli.Pdf&query=%24.score&label=openssf%20scorecard" alt="OpenSSF Scorecard"></a>
  <a href="https://www.nuget.org/packages/Rustaveli.Pdf"><img src="https://img.shields.io/nuget/v/Rustaveli.Pdf.svg" alt="NuGet"></a>
</p>

<p align="center">
  <a href="https://themidnightgospel.github.io/Rustaveli.Pdf/">Documentation</a>
  &nbsp;&nbsp;•&nbsp;&nbsp;
  <a href="#quick-start">Quick start</a>
  &nbsp;&nbsp;•&nbsp;&nbsp;
  <a href="https://themidnightgospel.github.io/Rustaveli.Pdf/guide/">Guides</a>
  &nbsp;&nbsp;•&nbsp;&nbsp;
  <a href="https://themidnightgospel.github.io/Rustaveli.Pdf/api/">API reference</a>
  &nbsp;&nbsp;•&nbsp;&nbsp;
  <a href="#benchmarks">Benchmarks</a>
  &nbsp;&nbsp;•&nbsp;&nbsp;
  <a href="https://www.nuget.org/packages/Rustaveli.Pdf">NuGet</a>
</p>
<!-- /github-only -->

| Free, for everyone | Fully featured | Plain .NET |
|---|---|---|
| MIT-licensed, for commercial and closed-source software, and it stays MIT. No revenue limits, keys or tiers. | Tables across pages, both text directions, complex scripts, PDF/A, PDF/UA, encryption and merging. | Managed code, no native dependencies. .NET 10 and .NET Standard 2.0 (.NET Framework 4.7.2 and later). |

## Quick start

```bash
dotnet add package Rustaveli.Pdf
```

<!-- github-only -->
<details>
<summary>Code</summary>
<!-- /github-only -->

```csharp
using Rustaveli.Pdf;

(string Item, string Amount)[] lines =
[
    ("Design review", "1,600.00"),
    ("Implementation", "7,200.00"),
    ("Support, one month", "450.00"),
];

Ink blue = Ink.Hex("#1565C0");

Document invoice = Document.Compose(composition => composition.Section(section =>
{
    section.Trim = PaperSizes.A5;
    section.Margins = Sides.All(40);
    section.DefaultType = TypeStyle.Default.WithPointSize(10);

    section.Body().Stack(stack =>
    {
        stack.SpaceBetween(20);

        stack.Add().Text(text =>
        {
            text.Line("Invoice INV-0042")
                .PointSize(22)
                .Bold()
                .Ink(blue);

            text.Line("Issued 29 September 2026, due in 30 days");
        });

        stack.Add().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Share();
                columns.Fixed(70);
            });

            table.HeaderRows(header =>
            {
                header
                    .Cell()
                    .Fill(blue)
                    .Inset(6)
                    .Text(text => text
                        .Run("Item")
                        .Bold()
                        .Ink(Ink.White));

                header
                    .Cell()
                    .Fill(blue)
                    .Inset(6)
                    .FlushRight()
                    .Text(text => text
                        .Run("Amount")
                        .Bold()
                        .Ink(Ink.White));
            });

            foreach ((string item, string amount) in lines)
            {
                table
                    .Cell()
                    .StrokeBottom(0.5f)
                    .Inset(6)
                    .Text(item);

                table
                    .Cell()
                    .StrokeBottom(0.5f)
                    .Inset(6)
                    .FlushRight()
                    .Text(amount);
            }
        });

        stack
            .Add()
            .FlushRight()
            .Text(text => text
                .Run("Total due 9,250.00")
                .PointSize(14)
                .Bold());
    });
}));

invoice.ExportPdf("invoice.pdf");
```

<!-- github-only -->
</details>
<!-- /github-only -->

![Invoice INV-0042, made by the code above](https://raw.githubusercontent.com/themidnightgospel/Rustaveli.Pdf/main/docs/images/invoice.png)

<!-- github-only -->
## Benchmarks

Measured again on every merge to main ([as text](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/benchmark-data/benchmarks/latest.md),
[history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/)):

<a href="https://github.com/themidnightgospel/Rustaveli.Pdf/blob/benchmark-data/benchmarks/latest.md">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/latest-dark.svg">
    <img src="https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/latest-light.svg" alt="The latest benchmarks: time, allocations and file size for each document, made by both libraries">
  </picture>
</a>
<!-- /github-only -->

## What it makes

<!-- github-only -->
<table>
  <tr>
    <td width="50%" valign="top">
      <img src="https://raw.githubusercontent.com/themidnightgospel/Rustaveli.Pdf/main/docs/images/readme/text-and-tables.png" alt="A page of styled text, framed and rounded boxes, lists and a table">
      <br><strong>Text, frames, lists and tables</strong>
      <br>Styled runs, rounded fills, numbered lists, tables with repeated headers.
    </td>
    <td width="50%" valign="top">
      <img src="https://raw.githubusercontent.com/themidnightgospel/Rustaveli.Pdf/main/docs/images/readme/scripts.png" alt="Arabic, English with Arabic, Devanagari and justified Arabic text">
      <br><strong>Every script, both directions</strong>
      <br>Arabic shaped and set right to left beside English, Devanagari, justified Arabic.
    </td>
  </tr>
</table>
<!-- /github-only -->

- **Typography**: OpenType features, kerning, fallback fonts per character, text in both directions, Arabic and
  Indic scripts.
- **Images and artwork**: JPEG and PNG embedded as they are, SVG and drawn paths kept as vectors.
- **Standards**: PDF/A-2 and A-3, tagged PDF and PDF/UA, AES-256, each checked by veraPDF and qpdf.
- **Existing files**: merge, stamp, attach electronic invoices, protect and linearise.
- **Live preview**: redraws as you edit, with an inspector that opens the code behind any frame.

## About this project

Rustaveli.Pdf sets out to give C# and .NET a complete PDF generation toolkit that is free for everyone, for good. It is
MIT-licensed, and every future release will be too.

It is built with an AI coding agent. A change is merged only after more than 10,000 tests pass, including veraPDF
validation of PDF/A and PDF/UA output. The parsers are fuzzed every night, and every merge is benchmarked.

QuestPDF's MIT-licensed releases are its reference: they inspired its design, and its tests and benchmarks compare
against their output. [Rustaveli.Pdf and QuestPDF](https://themidnightgospel.github.io/Rustaveli.Pdf/questpdf/) sets
out how the two libraries relate.

## Packages

| Package | Adds |
|---|---|
| [![Rustaveli.Pdf](https://img.shields.io/nuget/v/Rustaveli.Pdf.svg?label=Rustaveli.Pdf)](https://www.nuget.org/packages/Rustaveli.Pdf) | Layout, text, images and PDF export: everything most documents need |
| [![Rustaveli.Pdf.Raster](https://img.shields.io/nuget/v/Rustaveli.Pdf.Raster.svg?label=Rustaveli.Pdf.Raster)](https://www.nuget.org/packages/Rustaveli.Pdf.Raster) | Pages as PNG, JPEG or WebP, SVG pages and XPS; image recompression |
| [![Rustaveli.Pdf.Shaping](https://img.shields.io/nuget/v/Rustaveli.Pdf.Shaping.svg?label=Rustaveli.Pdf.Shaping)](https://www.nuget.org/packages/Rustaveli.Pdf.Shaping) | Arabic, Hebrew points, and Indic and South-East Asian scripts |
| [![Rustaveli.Pdf.Operations](https://img.shields.io/nuget/v/Rustaveli.Pdf.Operations.svg?label=Rustaveli.Pdf.Operations)](https://www.nuget.org/packages/Rustaveli.Pdf.Operations) | Existing PDF files: merging, stamping, attaching, protecting |
| [![Rustaveli.Pdf.Preview](https://img.shields.io/nuget/v/Rustaveli.Pdf.Preview.svg?label=Rustaveli.Pdf.Preview)](https://www.nuget.org/packages/Rustaveli.Pdf.Preview) | A live preview in the browser, redrawn on hot reload |

## Documentation

- [Guides](https://themidnightgospel.github.io/Rustaveli.Pdf/guide/), starting with
  [getting started](https://themidnightgospel.github.io/Rustaveli.Pdf/guide/getting-started/)
- [API reference](https://themidnightgospel.github.io/Rustaveli.Pdf/api/)
- [Features](https://themidnightgospel.github.io/Rustaveli.Pdf/features/) and
  [known limitations](https://themidnightgospel.github.io/Rustaveli.Pdf/limitations/)
- [Coming from QuestPDF](https://themidnightgospel.github.io/Rustaveli.Pdf/guide/coming-from-questpdf/), and how
  the two libraries relate: [Rustaveli.Pdf and QuestPDF](https://themidnightgospel.github.io/Rustaveli.Pdf/questpdf/)
- [Changelog](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/CHANGELOG.md)

## Licence

MIT — see [LICENSE](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/LICENSE). The bundled Noto Sans is
under the [SIL Open Font License](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/src/Rustaveli.Pdf/Fonts/Bundled/OFL.txt).
