# Coming from QuestPDF

Rustaveli.Pdf offers the capabilities QuestPDF offers — the [parity checklist](../parity/PARITY.md) follows each
one — through an API of its own. Both describe a document in C# as a fluent tree of content that breaks into pages
by itself, so the overall shape of your code carries over; the names, and a good many details, do not. This library
names things as print and page layout do ([ADR 0002](../adr/0002-print-vocabulary.md)): a page's size is its *trim*,
padding is an *inset*, a page number is a *folio*.

How the two libraries relate — the design inspiration and licensing — is set out in
[Rustaveli.Pdf and QuestPDF](../questpdf.md).

## The same ideas, by their print names

| QuestPDF | Rustaveli.Pdf |
|---|---|
| `Document.Create(container => ...)` | `Document.Compose(composition => ...)` |
| `container.Page(page => ...)` | `composition.Section(section => ...)` |
| `page.Size(...)`, `page.Margin(...)` | `section.Trim = ...`, `section.Margins = ...` |
| `page.PageColor(...)` | `section.Paper = ...` |
| `page.DefaultTextStyle(...)` | `section.DefaultType = ...` |
| `page.Header()`, `Content()`, `Footer()` | `section.RunningHead()`, `Body()`, `RunningFoot()` |
| `page.Background()`, `Foreground()` | `section.Underlay()`, `Overlay()` |
| `IContainer` | `IFrame` |
| `Column(column => column.Item()...)` | `Stack(stack => stack.Add()...)` |
| `Row(row => row.RelativeItem()...)` | `Columns(columns => columns.Share()...)` |
| `ConstantItem`, `AutoItem` | `Fixed`, `Natural` |
| `Padding`, `Background`, `Border` | `Inset`, `Fill`, `Stroke` |
| `AlignCenter`, `AlignMiddle` | `Centered`, `Middle` |
| `Width`, `Height`, `Extend`, `Shrink` | `Width`, `Height`, `Expand`, `FitToContent` |
| `PageBreak`, `ShowEntire`, `EnsureSpace` | `NewPage`, `KeepTogether`, `RequireSpace` |
| `ShowIf`, `ShowOnce`, `SkipOnce` | `When`, `Once`, `SkipFirst` |
| `Decoration`, `Layers`, `Inlined`, `MultiColumn` | `Banded`, `Layered`, `Flow`, `FlowColumns` |
| `Text(text => text.Span(...))` | `Text(text => text.Run(...))` |
| `CurrentPageNumber`, `TotalPages` | `Folio`, `PageCount` |
| `Section`, `SectionLink`, `Hyperlink` | `Anchor`, `CrossReference`, `Link` |
| `TextStyle`, `FontSize`, `LineHeight`, `LetterSpacing` | `TypeStyle`, `PointSize`, `Leading`, `Tracking` |
| `Color`, `Colors.Blue.Medium` | `Ink`, `Ink.Hex("#2196F3")` |
| `FontManager.RegisterFont` | `TypefaceLibrary.Shared.Register` |
| `IComponent`, `Dynamic`, `Lazy` | `ISnippet`, `ComposePerPage`, `ComposeLater` |
| `Placeholders` | `SampleData` |
| `GeneratePdf`, `GenerateImages`, `GenerateSvg`, `GenerateXps` | `ExportPdf`, `ExportImages`, `ExportSvg`, `ExportXps` |
| `DocumentOperation` | `PdfFile` |
| `ShowInCompanion`, `ShowInPreviewer` | `DocumentPreview.Preview` |
| `DocumentLayoutException` | `OversetException` |

The [glossary](../GLOSSARY.md) defines every public name, and the [parity checklist](../parity/PARITY.md) gives the
counterpart of each QuestPDF feature.

## What works differently

- **No licence to set.** The library is MIT-licensed, for any use; there is no `Settings.License`.
- **No global settings.** What QuestPDF sets once in `Settings` — fonts, image quality, PDF/A, caching, debugging —
  is given per export, in `PdfExportOptions` or `ImageExportOptions`. Two exports in one process can use different
  typefaces, and nothing needs setting before the first.
- **Debugging is always on**, at no cost until a layout fails: the failure names the frames down to the one that did
  not fit. `Named` gives a frame a name to be called by.
- **No built-in colour palette.** A document brings its colours as `Ink`s — RGB, CMYK or named spot inks with a
  process fallback. `Tint` gives a percentage of any ink.
- **The default typeface is Noto Sans, not Lato.** Both are carried in their packages, so a document that names no
  typeface looks the same everywhere with either — but Noto Sans sets about 6% wider, with taller lowercase, so text
  ported with its default type wraps a little differently and may take an extra line or page. For QuestPDF's exact
  line breaks, register Lato — free under the Open Font License — with `TypefaceLibrary.Shared.RegisterFile` and set
  it as the section's `DefaultType`: `TypeStyle.Default.WithTypeface("Lato")`.
- **A typeface nobody has is substituted in kind**: a missing sans-serif by an installed sans-serif, a serif by a
  serif, a monospace by a monospace, where QuestPDF sets all of them in Lato.
- **The core package has no native dependencies.** PDF is written in managed code; SkiaSharp is needed only for page
  images and image recompression (`Rustaveli.Pdf.Raster`), HarfBuzz only for complex scripts
  (`Rustaveli.Pdf.Shaping`). The core runs anywhere .NET does, .NET Framework included.
- **The preview runs in the browser** and is redrawn by `dotnet watch` hot reload; there is no separate application.
- **Page numbers per part** of a document come from anchors — `FolioWithin` and `PageCountOf` — or, for merged
  documents, from `NumberPartsSeparately`.

## An example, side by side in words

A statement with a running head, a table whose header repeats on every page, and page numbers in the foot:

```csharp
Document statement = Document.Compose(composition => composition.Section(section =>
{
    section.Trim = PaperSizes.A4;
    section.Margins = Sides.All(2.Centimetres());
    section.DefaultType = TypeStyle.Default.WithPointSize(10);

    section.RunningHead().InsetBottom(12).Text(text => text.Run("Statement of account").PointSize(16).Bold());

    section.Body().Table(table =>
    {
        table.Columns(columns =>
        {
            columns.Fixed(80);
            columns.Share();
            columns.Fixed(80);
        });

        table.HeaderRows(header =>
        {
            header.Cell().StrokeBottom(1).Text("Date");
            header.Cell().StrokeBottom(1).Text("Description");
            header.Cell().StrokeBottom(1).FlushRight().Text("Amount");
        });

        for (int day = 1; day <= 90; day++)
        {
            table.Cell().InsetVertical(3).Text(new DateTime(2026, 1, 1).AddDays(day - 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            table.Cell().InsetVertical(3).Text("Transfer " + day.ToString(CultureInfo.InvariantCulture));
            table.Cell().InsetVertical(3).FlushRight().Text((day * 12.5m).ToString("0.00", CultureInfo.InvariantCulture));
        }
    });

    section.RunningFoot().Centered().Text(text =>
    {
        text.Run("Page ");
        text.Folio();
        text.Run(" of ");
        text.PageCount();
    });
}));
```

Read against the table above, each line has a counterpart in the QuestPDF document you would have written: `Page`
became `Section`, `Header` became `RunningHead`, `PaddingBottom` became `InsetBottom`, `ColumnsDefinition` became
`Columns`, `BorderBottom` became `StrokeBottom`, `AlignRight` became `FlushRight`, and `CurrentPageNumber` became
`Folio`.
