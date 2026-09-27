# Glossary

The library's vocabulary, and where each word comes from. Names follow [ADR 0002](adr/0002-print-vocabulary.md):
familiar print and typesetting terms — what a user of InDesign, Word or LaTeX would recognise — and plain English
where print has no word. Every public type in the library appears here; a test fails when one does not.

The **Replaces** column records the name each concept had before this vocabulary was adopted, so earlier code and
the QuestPDF-derived names it used can be read across.

Everything public lives in the `Rustaveli.Pdf` namespace: one `using` is enough to write any document.

---

## The document

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Document` | class | A composed document, ready to export. | — | `Document` |
| `Document.Compose(Action<IComposition>)` | method | Builds a document. *Composition* is the old word for typesetting: compositors composed type into pages. | print | `Document.Create` |
| `IComposition` | interface | What a document is composed of: a sequence of sections. | print | `IDocumentContainer` |
| `IComposition.Section(Action<Section>)` | method | Adds a section: a run of pages sharing one page setup and running heads, as in Word and InDesign. | Word, InDesign | `IDocumentContainer.Page` |
| `Section` | class | A section's page setup and its frames. | Word, InDesign | `PageDescriptor` |
| `Section.Trim` | property | Page size — the *trim size* of the finished page. | print | `PageDescriptor.Size` |
| `Section.Margins` | property | Space between the trim edge and the text area. | print | `PageDescriptor.Margin` |
| `Section.Paper` | property | The colour of the paper, drawn behind everything. | InDesign ("Paper" swatch) | `PageDescriptor.BackgroundColor` |
| `Section.ReadingDirection` | property | Left-to-right or right-to-left. | print | `PageDescriptor.Direction` |
| `Section.DefaultType` | property | The type style text inherits unless told otherwise. | print ("body type") | `PageDescriptor.DefaultTextStyle` |
| `Section.Continuous` | property | One page that grows to fit its content, instead of paginating. | plain | `PageDescriptor.IsContinuous` |
| `Section.RunningHead()` | method | Repeated at the top of every page. | print | `PageDescriptor.Header()` |
| `Section.Body()` | method | The main text area, flowing across pages. | print ("body text") | `PageDescriptor.Content()` |
| `Section.RunningFoot()` | method | Repeated at the bottom of every page. | print | `PageDescriptor.Footer()` |
| `Section.Underlay()` | method | Drawn beneath everything, ignoring margins — watermarks, page furniture. | print | `PageDescriptor.Background()` |
| `Section.Overlay()` | method | Drawn above everything, ignoring margins. | print | `PageDescriptor.Foreground()` |
| `DocumentInfo` | class | Title, author, subject, keywords, creator, producer, dates. Named for the PDF *document information dictionary*. | PDF | `DocumentMetadata` |
| `Document.Info` | property | The document's `DocumentInfo`. | PDF | `Document.Metadata` |
| `ExportPdf()` | method | Writes the document as PDF, to bytes, a stream or a file. *Export* is what InDesign calls it. | InDesign | `GeneratePdf` |
| `PdfExportOptions` | class | Options for export: the `Typefaces` to set text in, and whether to `Compress` streams. | InDesign | `PdfGenerationOptions` |

## Frames and composing into them

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `IFrame` | interface | A place content is set into. Every method below that takes one returns another, so modifiers chain until content ends the chain. | InDesign | `IContainer` |
| `ISnippet` | interface | A reusable piece of composition, `Compose(IFrame)`. InDesign calls reusable content *snippets*. | InDesign | `IComponent` |
| `Snippet(ISnippet)` / `Snippet<T>()` | method | Places a snippet. | InDesign | `Component` |
| `Compose(Action<IFrame>)` | method | Composes into a frame with a method of your own. | print | `Element` |
| `Blank()` | method | Places nothing. | print | `Empty` |
| `FrameContent` | class | The methods that set content into a frame, and so end a chain: `Text`, `Image`, `Stack`, `Columns`, `Table`, `List`, `Layered`, `Banded`, `Compose`, `Snippet`, `Blank`. | plain | `ContentExtensions` |
| `FrameModifiers` | class | The methods that wrap a frame in another and return the inner one: every method under *Modifying a frame*, *Flow across pages* and *Rules, links and placeholders* below. | plain | `LayoutExtensions` |

## Arranging frames

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Stack(Action<StackComposer>)` | method | Frames one above the other, flowing across pages. | plain | `Column` |
| `StackComposer.Add()` | method | Adds the next frame to the stack. | plain | `ColumnDescriptor.Item` |
| `StackComposer.SpaceBetween(float)` | method | Vertical space between stacked frames. | print ("space between") | `ColumnDescriptor.Spacing` |
| `Columns(Action<ColumnsComposer>)` | method | Frames side by side. In page layout, content set side by side is set in columns. | print | `Row` |
| `ColumnsComposer.Share(float)` | method | A column taking a share of the width left over, in proportion to its weight. | plain | `RowDescriptor.RelativeItem` |
| `ColumnsComposer.Fixed(float)` | method | A column of a fixed width. | plain | `RowDescriptor.ConstantItem` |
| `ColumnsComposer.Natural()` | method | A column as wide as its content. | plain | `RowDescriptor.AutoItem` |
| `ColumnsComposer.Gutter(float)` | method | Space between columns. | print | `RowDescriptor.Spacing` |
| `Table(Action<TableComposer>)` | method | Rows and columns of cells, paginated at row boundaries. | print | `Table` |
| `TableComposer.Columns(Action<TableColumns>)` | method | Defines the table's columns. | print | `ColumnsDefinition` |
| `TableColumns.Fixed(float)` / `Share(float)` | method | A fixed-width or proportional column. | plain | `ConstantColumn` / `RelativeColumn` |
| `TableComposer.Cell()` | method | The next cell, placed automatically or explicitly. | print | `Cell` |
| `TableComposer.HeaderRows(...)` / `FooterRows(...)` | method | Rows repeated at the top or bottom of every page the table spans. | InDesign | `Header` / `Footer` |
| `TableBand` | class | The cells of a header or footer band. | print | `TableBandDescriptor` |
| `CellFrame` | class | A cell's frame, with its placement: `AtRow`, `AtColumn`, `SpanRows`, `SpanColumns`. | print | `TableCellDescriptor` |
| `List(Action<ListComposer>)` | method | A bulleted or numbered list. | print | `List` |
| `ListComposer.Bulleted()` / `Numbered(ListNumbering)` | method | The kind of list. | print | `Unordered` / `Ordered` |
| `ListComposer.Add()` | method | Adds the next item. | plain | `Item` |
| `ListComposer.SpaceBetween(float)` | method | Vertical space between items. | print | `Spacing` |
| `ListComposer.MarkerIndent(float)` | method | The hanging indent the marker sits in. | print | `MarkerWidth` |
| `ListComposer.MarkerType(Func<TypeStyle, TypeStyle>)` | method | How markers are set. | print | `MarkerStyle` |
| `ListNumbering` | enum | `Bullet`, `Arabic`, `LowerAlpha`, `UpperAlpha`, `LowerRoman`, `UpperRoman`. | print | `ListMarker` |
| `Layered(Action<LayersComposer>)` | method | Frames drawn over one another. | InDesign | `Layers` |
| `LayersComposer.BaseLayer()` / `Layer()` | method | The layer that sizes the stack of layers, and any other. | InDesign | `PrimaryLayer` / `Layer` |
| `Banded(Action<BandsComposer>)` | method | Content with a head band and a foot band repeated on every page it spans. | print | `Decoration` |
| `BandsComposer.Head()` / `Body()` / `Foot()` | method | The bands and the content between them. | print | `Before` / `Content` / `After` |

## Modifying a frame

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Inset(...)`, `InsetLeft/Top/Right/Bottom/Horizontal/Vertical` | method | Space inside a frame's edge. InDesign calls it the *text frame inset*. | InDesign | `Padding*` |
| `Fill(Ink)` | method | Paints the frame's whole area. | InDesign | `Background` |
| `Stroke(float)`, `StrokeLeft/Top/Right/Bottom` | method | A line around the frame's edge, of a given weight. | InDesign | `Border*` |
| `StrokeInk(Ink)` | method | The ink a stroke is drawn in. | InDesign | `BorderColor` |
| `RoundCorners(float)` | method | Rounds the frame's corners. | InDesign ("corner options") | `CornerRadius` |
| `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight` | method | Size constraints. | plain | same |
| `Expand()`, `ExpandHorizontally()`, `ExpandVertically()` | method | Claims all the space offered. | plain | `Extend*` |
| `Proportion(float, ProportionFit)` | method | Holds a width-to-height ratio. | print | `AspectRatio` |
| `ProportionFit` | enum | `Width`, `Height`, `Area`: which dimension the ratio is fitted to. | plain | `AspectRatioOption` |
| `FlushLeft()`, `FlushRight()`, `Centered()` | method | Horizontal placement within the frame. | print | `AlignLeft/Right/Center` |
| `FlushTop()`, `FlushBottom()`, `Middle()` | method | Vertical placement within the frame. | print | `AlignTop/Bottom/Middle` |
| `Unbounded()` | method | Lays content out free of the space offered. | plain | `Unconstrained` |
| `ShiftAcross(float)`, `ShiftDown(float)` | method | Moves drawn content without affecting layout. | InDesign ("shift") | `TranslateX/Y` |
| `Scale(...)` | method | Scales content. | print | `Scale` |
| `ShrinkToFit(float)` | method | Scales content down until it fits. | Word | `ScaleToFit` |
| `TurnLeft()`, `TurnRight()` | method | A quarter turn. | plain | `RotateLeft/Right` |
| `MirrorHorizontal()`, `MirrorVertical()`, `MirrorBoth()` | method | Reflects content. | print | `FlipHorizontal/Vertical/Over` |
| `LeftToRight()`, `RightToLeft()`, `Reading(ReadingDirection)` | method | Reading direction for the frame and its content. | print | `ContentFrom` |
| `DefaultType(Func<TypeStyle, TypeStyle>)` | method | Refines the type style content inherits. | print | `DefaultTextStyle` |

## Flow across pages

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `NewPage()` | method | Starts the next page. | print ("start on next page") | `PageBreak` |
| `KeepTogether()` | method | Never splits the frame across pages. | print | `ShowEntire` |
| `RequireSpace(float)` | method | Starts a new page unless at least this much space remains. | plain | `EnsureSpace` |
| `When(bool)` | method | Includes the frame only when the condition holds. | plain | `ShowIf` |
| `Once()` | method | Drawn only the first time, even in a repeating band. | plain | `ShowOnce` |
| `SkipFirst()` | method | Drawn every time but the first — "continued" labels. | plain | `SkipOnce` |

## Rules, links and placeholders

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Rule(float, Ink?)` | method | A horizontal line. | print | `LineHorizontal` |
| `VerticalRule(float, Ink?)` | method | A vertical line — between columns, say. | print | `LineVertical` |
| `Link(string url)` | method | Makes the frame a link to a URL. | plain | `Hyperlink` |
| `Anchor(string name)` | method | Names a place others can refer to. | print, InDesign ("text anchor") | `Section` |
| `CrossReference(string anchor)` | method | Makes the frame a link to an anchor. | print | `SectionLink` |
| `Placeholder(Ink?)` | method | A box standing in for content not there yet. | print | `Placeholder` |
| `Image(IImage, ImageFitting)` | method | Places an image. | plain | `Image` |
| `IImage` | interface | An image a frame can place, with its size in pixels the right way up. | plain | `IImage` |
| `RasterImage` | class | A JPEG or PNG, loaded with `FromBytes`, `FromStream` or `FromFile` and embedded as it was encoded wherever PDF allows. *Raster*, as prepress distinguishes pixel images from vector art. | print | `Image` |
| `ImageFitting` | enum | `FitWidth`, `FitHeight`, `Proportionally`, `Stretch`, after InDesign's fitting options. | InDesign | `ImageFit` |

## Text

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Text(string)` / `Text(Action<TextComposer>)` | method | A paragraph. | plain | `Text` |
| `TextComposer.Run(string)` | method | A run of text in one style. | typography | `Span` |
| `TextComposer.Line(string)` / `BlankLine()` | method | A run followed by a line break; an empty line. | plain | `Line` / `EmptyLine` |
| `TextComposer.Folio()` | method | The current page number. *Folio* is the printer's word for a page number. Each folio method also takes a `Func<int, string>` that writes the number. | print | `CurrentPageNumber` |
| `TextComposer.PageCount()` | method | The number of pages. | plain | `TotalPages` |
| `TextComposer.FolioOf(string anchor)` | method | The page an anchor begins on. | print | `PageNumberOfSection` |
| `TextComposer.LastFolioOf(string anchor)` | method | The page an anchored frame's content ends on. | print | `EndPageNumberOfSection` |
| `TextComposer.FolioWithin(string anchor)` | method | This page's number counted from the page an anchor begins on. | print | `PageNumberWithinSection` |
| `TextComposer.PageCountOf(string anchor)` | method | How many pages an anchored frame's content spans. | plain | `TotalPagesWithinSection` |
| `Numerals` | class | Writes numbers as folios and lists are set: `Arabic`, `UpperRoman`, `LowerRoman`, `UpperAlpha`, `LowerAlpha`, or in a list's `Format`. | print | `FormatAsRoman` and friends |
| `TextComposer.Link(text, url)` / `CrossReference(text, anchor)` | method | Linked runs. | print | `Hyperlink` / `SectionLink` |
| `TextComposer.Inline(Action<IFrame>, InlinePosition)` | method | A frame set inline with the text. | print ("inline graphic") | `Element` |
| `InlinePosition` | enum | `OnBaseline`, `BelowBaseline`, `TextTop`, `TextBottom`, `Middle`: where an inline frame sits against its line. | CSS (`vertical-align`) | `TextInjectedElementAlignment` |
| `TextComposer.FlushLeft()`, `FlushRight()`, `Centered()` | method | Paragraph alignment. | print | `AlignLeft/Right/Center` |
| `TextComposer.FlushStart()`, `FlushEnd()` | method | Paragraph alignment by reading direction: flush against the edge lines start from, or end at. | print | `AlignStart/End` |
| `TextComposer.Justified()` | method | Stretches every line but a paragraph's last across the width by widening its word spaces. | print | `Justify` |
| `TextComposer.MaxLines(int, string)` | method | Shows at most so many lines, the last cut back to end in an ellipsis. | plain | `ClampLines` |
| `TextComposer.FirstLineIndent(float)` | method | Indents the first line of each paragraph. | print | same |
| `TextComposer.SpaceBetweenParagraphs(float)` | method | Space after each paragraph but the last. | print | `ParagraphSpacing` |
| `TextComposer.DefaultType(...)` | method | Refines the type style runs inherit. | print | `DefaultTextStyle` |
| `RunComposer` | class | Styles a run: `Typeface`, `PointSize`, `Ink`, `Highlight`, `Weight`, `Bold`, `Italic`, `Underline`, `StrikeThrough`, `Overline`, `StrokeStyle`, `StrokeInk`, `StrokeWeight`, `Leading`, `Tracking`, `WordSpacing`, `BreakAnywhere`, `LeftToRight`, `RightToLeft`, `Feature`, `Ligatures`, `SmallCapitals`, `OldstyleFigures`, `TabularFigures`, `Subscript`, `Superscript`, `Style`. | print | `TextSpanDescriptor` |
| `TypeStyle` | class | How type is set: typeface, point size, weight, italic, ink, highlight, leading, tracking, word spacing, script position, underline, strike-through, overline and how those strokes are drawn, whether lines may break anywhere within it, and the direction it reads in when set apart from the text around it. | print ("type style") | `TextStyle` |
| `TypeStyle.WithTypeface`, `WithPointSize`, `WithInk`, … | method | A copy with one attribute changed. | plain | `FontFamilyOf`, `FontSizeOf`, `ColorOf`, … |
| `TypeStyle.WithFeature(string, int)` | method | Turns an OpenType feature on, off or to an alternate by its four-letter tag; `Ligatures`, `SmallCapitals`, `OldstyleFigures` and `TabularFigures` name the common ones. | print (OpenType) | `EnableFontFeature` |
| `Typeface` | term | A font family, such as Noto Sans. | print | `FontFamily` |
| `PointSize` | term | Type size in points. | print | `FontSize` |
| `Leading` | term | Line spacing, as a multiple of the point size. | print | `LineHeight` |
| `Tracking` | term | Uniform extra space between letters. | print | `LetterSpacing` |
| `Highlight` | term | A colour behind a run of text. | plain | `BackgroundColor` |
| `TypeWeight` | enum | `Thin` … `Black`, `ExtraBlack`. | print | `FontWeight` |
| `ScriptPosition` | enum | `Normal`, `Subscript`, `Superscript`. | print | `FontPosition` |
| `StrokeStyle` | enum | `Solid`, `Double`, `Dotted`, `Dashed`, `Wavy`: how a text stroke (underline, strike-through, overline) is drawn. | print | `TextStyle.Decoration*` |

## Colour

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Ink` | struct | A colour as print thinks of it: RGB, CMYK process colour, or a named spot ink with a process fallback ([ADR 0004](adr/0004-ink-colour-model.md)). | print | `Color` |
| `Ink.Rgb`, `Ink.Cmyk`, `Ink.Spot`, `Ink.Hex` | method | Creates an ink. | print | `Color.FromArgb`, `Color.ParseHex` |
| `Ink.Tint(float)` | method | A percentage of the ink, as a printer lays down less of it. | print | — |
| `Ink.Black`, `White`, `Transparent`, `Registration` | property | The named inks. *Registration* prints on every separation, for crop and registration marks. | print | `Colors.Black/White/Transparent` |
| `InkModel` | enum | `Rgb`, `Cmyk`, `Spot`. | print | — |

The Material Design palette (`Colors.Red.Lighten3` and friends) is removed: users bring their own colours.

## Measurement and geometry

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Extent` | struct | A width and a height, in points. | plain | `Size` |
| `Offset` | struct | A displacement or position, in points. | plain | `Position` |
| `Sides` | struct | Four values, one per side — margins, insets, stroke weights. | plain | `Edges` |
| `PaperSizes` | class | ISO A, B and C series, US and architectural sizes, envelopes. | print | `PageSizes` |
| `Landscape()` / `Portrait()` | method | An extent turned to its wide or tall orientation. | print | same |
| `LengthUnit` | enum | `Point`, `Millimetre`, `Centimetre`, `Metre`, `Inch`, `Foot`. | plain | `Unit` |
| `Lengths` | class | Conversions such as `20.Millimetres()`. | plain | `UnitExtensions` |
| `ReadingDirection` | enum | `LeftToRight`, `RightToLeft`. | print | `ContentDirection` |

## Typefaces and export

PDF is written in managed code ([ADR 0001](adr/0001-managed-pdf-writer.md)): fonts are subset to the glyphs a
document uses, and images are embedded as they were encoded wherever PDF can carry them.

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `TypefaceLibrary` | class | The typefaces documents are set in: those registered with it, then those installed. `Shared` serves any export given no library of its own. | print ("type library") | `FontManager`, `SkiaFontProvider` |
| `TypefaceLibrary.Register(...)` / `RegisterFile(string)` | method | Adds every face in a font file. A registered typeface shadows an installed one of the same name. | plain | `FontManager.RegisterFont` |
| `TypefaceLibrary.Fallbacks` | property | Typefaces tried in order for a character a run's own typeface lacks. | plain | `FallbackFamilies` |
| `PdfExport` | class | The `ExportPdf` methods, to bytes, a stream or a file. | InDesign | `PdfGenerationExtensions` |
| `ImageExport` | class | The `ExportImages` methods of the `Rustaveli.Pdf.Raster` package: every page as an image, drawn by SkiaSharp from the same layout and glyphs as the PDF. | InDesign ("Export JPEG") | `GenerateImages` |
| `ImageExportOptions` | class | The `Resolution` in pixels per inch, the `Format`, the `Quality` of lossy formats, and the `Typefaces`. | InDesign | `ImageGenerationSettings` |
| `PageImageFormat` | enum | `Png`, `Jpeg`, `Webp`. | plain | `ImageFormat` |

## Failures

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `TypesettingException` | class | Base of every failure raised while typesetting. | print | — |
| `OversetException` | class | Content that cannot fit, even on an empty page. In print, text that does not fit its frame is *overset*. | InDesign | `DocumentLayoutException` |
| `RenderingException` | class | A failure while drawing a page, carrying the page number. | plain | `DocumentDrawingException` |
| `CompositionException` | class | A failure while composing the document, before layout. | print | `DocumentComposeException` |

## Inside the engine

Not public; listed so the codebase speaks one language throughout.

| Name | Meaning | Replaces |
|---|---|---|
| `Block` | Anything laid out on a page. | `Element` |
| `EnclosingBlock` | A block with one child. | `ContainerElement` |
| `Fit` | What a block would do with the space offered: `Nothing`, `Defer`, `Partial` or `Complete`. Fitting copy to space is *copyfitting*. | `SpacePlan` (`Empty`, `Wrap`, `PartialRender`, `FullRender`) |
| `Plan(Extent, PlanContext)` | Reports the `Fit` for a space, without side effects. | `Measure` |
| `Render(Extent, RenderContext)` | Draws the block at the size its parent allots ([ADR 0012](adr/0012-parents-allot-final-size.md)). | `Draw` |
| `PlanContext` / `RenderContext` | What planning and rendering need to know. | `LayoutContext` / `DrawContext` |
| `Pagination` | Page numbers, the page count and anchors, across passes. | `PageContext` |
| `Typesetter` | Paginates a document and drives rendering. | `DocumentGenerator`, `DocumentRenderer` |
| `ISurface` / `IPageSink` | What a backend draws on, and receives pages through. | `ICanvas` / `IDocumentCanvas` |
| `ITypeMeasurer` / `TypeMetrics` | Measures type for layout. | `ITextMeasurer` / `FontMetrics` |
| `TextRun` | A run of text inside a paragraph. | `TextSpan` |
| `HorizontalPlacement` / `VerticalPlacement` | Left, centre, right; top, middle, bottom. | `HorizontalAlignment` / `VerticalAlignment` |
| `IFrameSlot` | The block a public `IFrame` holds, out of the caller's reach. | `IContainer.Child` |
| `*Block` | Each element, named after what it does: `FillBlock`, `StrokeBlock`, `StackBlock`, `ColumnsBlock`, … | `*Element` |
