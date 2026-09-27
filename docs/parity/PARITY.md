# Feature parity checklist

What it takes to be a complete alternative to QuestPDF: every capability its last MIT release (2026.5.0) offers,
whether we have it, and the phase that delivers it. The reference column names QuestPDF's public API so coverage
can be audited against it; our own names come from [the glossary](../GLOSSARY.md) and will differ by design
([ADR 0002](../adr/0002-print-vocabulary.md)).

Source: `dotnet run eng/parity-surface.cs`, which enumerates the oracle's public API by reflection
([ADR 0008](../adr/0008-clean-room.md)). Rerun it and diff against this file when auditing.

**Status:** ✅ have · 🟡 partial · ❌ missing · ➖ deliberately not matched (reason given)

**Phases:** 1 vocabulary · 2 managed writer · 3 text engine · 4 layout and styling · 5 images and SVG ·
6 output and conformance · 7 document operations · 8 preview tool · 9 docs and 1.0

---

## Document and page

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Build a document from pages; reusable document classes | `Document.Create`, `IDocument` | ✅ | — |
| Page size, named sizes, landscape/portrait | `PageDescriptor.Size`, `PageSizes`, `PageSizeExtensions` | 🟡 A0–A6, Letter, Legal, Tabloid, Executive; missing A7–A10, B, C, ARCH, envelopes, postcard | 4 |
| Margins, per side | `Margin*` | ✅ | — |
| Continuous (single tall) page | `ContinuousSize` | ✅ | — |
| Page size bounded by content (min/max) | `MinSize`, `MaxSize` | ❌ | 4 |
| Header, footer, content, background, foreground slots | `Header`, `Footer`, `Content`, `Background`, `Foreground` | ✅ | — |
| Page colour | `PageColor` | ✅ | — |
| Page-level default text style and direction | `DefaultTextStyle`, `ContentFrom*` | ✅ | — |
| Units: pt, mm, cm, inch, plus metre, feet, mil | `Unit` | 🟡 pt, mm, cm, inch | 4 |
| Metadata: title, author, subject, keywords, creator, producer, dates | `DocumentMetadata` | ✅ | — |
| Document language | `DocumentMetadata.Language` | ❌ | 6 |
| Merge generated documents, continuous or original numbering | `Document.Merge`, `MergedDocument` | ❌ | 7 |

## Layout

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Vertical sequence with spacing | `Column`, `Stack` | ✅ | — |
| Horizontal sequence: constant, relative, auto items, spacing | `Row` | ✅ | — |
| Tables: constant/relative columns, auto and explicit placement, spans, header/footer bands | `Table` | ✅ | — |
| Table: stretch last cells to table bottom | `ExtendLastCellsToTableBottom` | ❌ | 4 |
| Uniform grid | `Grid` | ❌ | 4 |
| Flow of inline items with wrapping, alignment, baseline | `Inlined` | ❌ | 4 |
| Newspaper columns, balanced | `MultiColumn` | ❌ | 4 |
| Stacked layers, one primary | `Layers` | ✅ | — |
| Content with repeating before/after bands | `Decoration` | ✅ | — |
| Bulleted, numbered, lettered and roman lists | — | ✅ ours only | — |
| Padding, per side | `Padding*` | ✅ | — |
| Width/height, min/max | `Width`, `MinWidth`, … | ✅ | — |
| Extend to fill | `Extend*` | ✅ | — |
| Shrink to content | `Shrink*`, `MinimalBox` | ❌ | 4 |
| Aspect ratio | `AspectRatio` | ✅ | — |
| Alignment, horizontal and vertical | `Align*` | 🟡 alignment expands to fill (README known limitation) | 4 |
| Unconstrained | `Unconstrained` | ✅ | — |
| Z-order | `ZIndex` | ❌ | 4 |

## Transforms

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Translate | `TranslateX/Y` | ✅ | — |
| Scale, uniform and per axis | `Scale`, `ScaleHorizontal/Vertical` | ✅ | — |
| Scale to fit | `ScaleToFit` | ✅ | — |
| Quarter-turn rotation | `RotateLeft/Right` | ✅ | — |
| Arbitrary-angle rotation | `Rotate(angle)` | ❌ | 4 |
| Flip | `Flip*` | ✅ | — |

## Flow control and pagination

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Page break | `PageBreak` | ✅ | — |
| Show if (static condition) | `ShowIf(bool)` | ✅ | — |
| Show if (per page: page number, total pages) | `ShowIf(Predicate<ShowIfContext>)` | ❌ | 4 |
| Show once, skip once | `ShowOnce`, `SkipOnce` | ✅ | — |
| Keep together / never split | `ShowEntire`, `PreventPageBreak` | 🟡 `ShowEntire` only | 4 |
| Ensure space | `EnsureSpace` | ✅ | — |
| Repeat on every page | `Repeat` | ❌ | 4 |
| Stop paging (draw first page only) | `StopPaging` | ❌ | 4 |
| Lazy composition for very large documents, optionally cached | `Lazy`, `LazyWithCache` | ❌ | 4 |
| Reusable components | `IComponent`, `Component` | ✅ | — |
| Dynamic components with state, per-page composition | `IDynamicComponent`, `Dynamic`, `DynamicContext` | ❌ | 4 |
| Capture a content position; query it later | `CaptureContentPosition`, `GetContentCapturedPositions` | ❌ | 4 |

## Styling

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Solid background | `Background` | ✅ | — |
| Linear-gradient background | `BackgroundLinearGradient` | ❌ | 4 |
| Borders per side, colour | `Border*`, `BorderColor` | ✅ | — |
| Border alignment inside/middle/outside | `BorderAlignment*` | ❌ | 4 |
| Linear-gradient border | `BorderLinearGradient` | ❌ | 4 |
| Corner radius, uniform | `CornerRadius` | ✅ | — |
| Corner radius per corner | `CornerRadiusTopLeft`, … | ❌ | 4 |
| Box shadow (blur, spread, offset, colour) | `Shadow`, `BoxShadowStyle` | ❌ | 4 |
| Lines: thickness | `LineHorizontal/Vertical` | ✅ | — |
| Lines: colour, dash pattern, gradient | `LineDescriptor` | ❌ | 4 |
| Colour: RGB(A), hex | `Color` | ✅ | — |
| Colour: CMYK and spot inks, tints | — | ✅ ours only: process colour, separations with a fallback | — |
| Colour palette | `Colors` (Material) | ➖ replaced by `Ink` basics ([ADR 0004](../adr/0004-ink-colour-model.md)) | 1 |
| Named style sheets | — | ❌ ours only ([ADR 0003](../adr/0003-api-shape-and-style-sheets.md)) | 4 |
| Placeholder box | `Placeholder` | 🟡 no label text | 4 |
| Sample data for prototyping | `Placeholders` (lorem ipsum, dates, prices, …) | ❌ | 4 |

## Text

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Spans with style; plain text; object text | `Text`, `Span`, `Line`, `EmptyLine` | ✅ | — |
| Font family, size, colour, background | | ✅ | — |
| Font family fallback list | `FontFamily(params string[])`, `Fallback` | ✅ per style, then per library | — |
| Weights Thin…Black | `Thin` … `Black` | ✅ | — |
| Weight ExtraBlack (950) | `ExtraBlack` | ✅ | — |
| Italic | `Italic` | ✅ | — |
| Underline, strikethrough | `Underline`, `Strikethrough` | ✅ | — |
| Overline | `Overline` | ✅ | — |
| Decoration style (solid, double, dotted, dashed, wavy), colour, thickness | `Decoration*` | ✅ placed by the font's own metrics | — |
| Line height, letter spacing | `LineHeight`, `LetterSpacing` | ✅ | — |
| Word spacing | `WordSpacing` | ✅ | — |
| Subscript, superscript | `Subscript`, `Superscript` | ✅ | — |
| Alignment left/centre/right | `AlignLeft`, … | ✅ | — |
| Alignment start/end (direction-aware) | `AlignStart`, `AlignEnd` | ✅ | — |
| Justify | `Justify` | ✅ | — |
| Clamp to N lines with ellipsis | `ClampLines` | ✅ | — |
| First-line indent, paragraph spacing | `ParagraphFirstLineIndentation`, `ParagraphSpacing` | ✅ | — |
| Break anywhere | `WrapAnywhere` | ✅ | — |
| Unicode line breaking (UAX #14) | — | ✅ Unicode 16, default rules | — |
| Per-span direction; bidi reordering | `Direction*` | ✅ UAX #9, runs isolated in their own direction | — |
| OpenType features (ligatures, small caps, figures, …) | `EnableFontFeature`, `FontFeatures` | ✅ every GSUB lookup type, per script | — |
| Complex-script shaping | — (built in) | ✅ `Shaping` package (HarfBuzz) | — |
| Inline elements in a paragraph, with vertical alignment | `Element(TextInjectedElementAlignment)` | ✅ | — |
| Current page, total pages | `CurrentPageNumber`, `TotalPages` | ✅ | — |
| Page number formatting (roman, custom) | `TextPageNumberDescriptor.Format` | ✅ | — |
| Section page numbers: begin, end, within, total within | `BeginPageNumberOfSection`, … | ✅ | — |
| Page number of a captured location | `PageNumberOfLocation` | ❌ | 4 |
| Glyph-availability check | `Settings.CheckIfAllTextGlyphsAreAvailable` | ✅ per export | — |

## Links and navigation

| Capability | Reference | Status | Phase |
|---|---|---|---|
| External hyperlink on a region or text | `Hyperlink`, `ExternalLink` | ✅ | — |
| Internal link to a named section | `SectionLink`, `InternalLink` | ✅ | — |
| Named destinations | `Section`, `Location` | ✅ | — |
| Links to captured locations from text | `ExternalLocation`, `InternalLocation` | ❌ | 4 |
| Outline / bookmarks | — (derived from semantic tags) | ❌ | 6 |

## Images and vector graphics

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Image from bytes, stream, file; shared image object | `Image`, `Image.From*` | ✅ | — |
| Fit width, height, area, unproportional | `ImageScaling`, `Fit*` | ✅ | — |
| Per-image compression quality and target DPI | `WithCompressionQuality`, `WithRasterDpi` | 🟡 document-wide only | 5 |
| Keep original image bytes | `UseOriginalImage` | ✅ always, wherever PDF can carry the encoding | — |
| Dynamic images generated at the final size | `GenerateDynamicImageDelegate` | ❌ | 5 |
| SVG, static and dynamic | `Svg`, `SvgImage` | ❌ | 5 |
| Custom vector drawing | `Canvas(DrawOnCanvas)` | ❌ own drawing API, not a raw Skia canvas | 5 |

## Fonts

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Register fonts from stream, file, embedded resource, custom name | `FontManager.Register*` | 🟡 bytes, stream, file (`TypefaceLibrary`); custom name ❌ | 2, 4 |
| System font discovery; extra discovery paths; opt out | `Settings.UseEnvironmentFonts`, `FontDiscoveryPaths` | 🟡 managed discovery; opt out per library; extra paths ❌ | 2, 4 |
| Font subsetting | — (built in) | ✅ TrueType subset, CFF whole | — |
| Bundled default font | Lato | ✅ Noto Sans subsets, Latin/Greek/Cyrillic | — |
| Fallback per character | built in | ✅ named fallbacks, registered, installed, bundled | — |
| Pair kerning | built in (HarfBuzz) | ✅ `GPOS` and `kern` | — |

## Output

| Capability | Reference | Status | Phase |
|---|---|---|---|
| PDF to bytes, stream, file | `GeneratePdf` | ✅ | — |
| Generate and open in the default viewer | `GeneratePdfAndShow` | ❌ | 6 |
| Page images: PNG, JPEG, WebP, DPI, quality | `GenerateImages`, `ImageGenerationSettings` | ✅ `ExportImages` in `Rustaveli.Pdf.Raster` | — |
| Parallel generation | built in | ✅ no process-wide lock | — |
| SVG pages | `GenerateSvg` | ❌ | 6 |
| XPS | `GenerateXps` | ❌ | 6 |
| Stream compression switch | `DocumentSettings.CompressDocument` | ✅ `PdfExportOptions.Compress` | — |
| PDF/A-2b | `PDFA_Conformance.PDFA_2B` | 🟡 prerequisites only | 6 |
| PDF/A-2a, 2u, 3a, 3b, 3u | `PDFA_Conformance` | ❌ | 6 |
| PDF/UA-1 with semantic tagging (headings, lists, tables, figures, alt text, language, artifacts) | `PDFUA_Conformance`, `Semantic*`, `AsSemanticHorizontalHeader` | ❌ | 6 |

## Document operations

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Load an existing PDF, with password | `DocumentOperation.LoadFile` | ❌ | 7 |
| Merge files | `MergeFile` | ❌ | 7 |
| Select pages | `TakePages` | ❌ | 7 |
| Overlay and underlay with page mapping | `OverlayFile`, `UnderlayFile`, `LayerConfiguration` | ❌ | 7 |
| Attachments with relationship (ZUGFeRD / Factur-X) | `AddAttachment`, `DocumentAttachment` | ❌ | 7 |
| Encrypt 40/128/256-bit with permissions; decrypt; remove restrictions | `Encrypt`, `Decrypt`, `RemoveRestrictions` | ❌ | 7 |
| Linearise (fast web view) | `Linearize` | ❌ | 7 |
| Extend XMP metadata | `ExtendMetadata` | ❌ | 7 |

## Diagnostics and tooling

| Capability | Reference | Status | Phase |
|---|---|---|---|
| Layout errors raised with explanation | `DocumentLayoutException` | ✅ | — |
| Visual debug areas and pointers | `DebugArea`, `DebugPointer` | ❌ | 8 |
| Debug mode with element-tree trace of a layout failure | `Settings.EnableDebugging` | ❌ | 8 |
| Layout exception threshold | `DocumentLayoutExceptionThreshold` | 🟡 fixed page cap | 4 |
| Caching switch | `Settings.EnableCaching` | ➖ no global switch; caching is internal | 2 |
| Live previewer with hot reload, element inspector, source navigation | `ShowInCompanion`, `ShowInPreviewer` | ❌ | 8 |
| Licence selection | `Settings.License`, `LicenseType` | ➖ MIT, nothing to select | — |
