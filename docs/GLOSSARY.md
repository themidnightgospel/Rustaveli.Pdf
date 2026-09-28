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
| `Document.Merge(params Document[])` | method | One document of every page of several, numbered on from one to the next. | plain | `Document.Merge`, `MergedDocument` |
| `Document.NumberPartsSeparately()` | method | Numbers each merged document's pages from 1, counting only its own. | print ("section numbering") | `UseOriginalPageNumbers` |
| `DocumentPreview` | class | Shows a document in the browser as it is written, in `Rustaveli.Pdf.Preview`: `Preview(compose)` until stopped, `StartPreview(compose)` for a `PreviewSession`. | plain ("preview") | `ShowInPreviewer`, `ShowInCompanion` |
| `PreviewSession` | class | A document served for preview at its `Url`, composed again on `Refresh` or hot reload, until disposed. | plain | the companion app |
| `PreviewOptions` | class | The preview's `Port`, drawing `Resolution`, whether to `OpenBrowser`, and `Typefaces`. | plain | — |
| `ShowFrameEdges(string?, Ink?)` | method | Draws the frame's edges as a dashed outline over its content, labelled, to see where frames lie. | InDesign ("show frame edges") | `DebugArea` |
| `Named(string)` | method | Names the frame, so a layout failure inside it says where by that name. | plain | `DebugPointer` |
| `StyleSheet` | class | A document's named styles — `DefineType`, `DefineParagraph`, `DefineFrame` — each able to build on another `basedOn` it; the document's is `Document.Styles`, also `IComposition.Styles` while composing. | InDesign ("character, paragraph and object styles") | — |
| `Style(string)` | method | Applies a named style: a type style to a run, a paragraph style to a block of text, a frame style to a frame. | InDesign | — |
| `Document.PageLimit` | property | The most pages a document may take before content that never stops asking for another is taken to be a fault. | plain | `Settings.DocumentLayoutExceptionThreshold` |
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
| `Section.MinimumTrim`, `Section.MaximumTrim` | property | Bounds a page sized by its content: no smaller than the one, no larger than the other. | print | `PageDescriptor.MinSize`, `MaxSize` |
| `Section.RunningHead()` | method | Repeated at the top of every page. | print | `PageDescriptor.Header()` |
| `Section.Body()` | method | The main text area, flowing across pages. | print ("body text") | `PageDescriptor.Content()` |
| `Section.RunningFoot()` | method | Repeated at the bottom of every page. | print | `PageDescriptor.Footer()` |
| `Section.Underlay()` | method | Drawn beneath everything, ignoring margins — watermarks, page furniture. | print | `PageDescriptor.Background()` |
| `Section.Overlay()` | method | Drawn above everything, ignoring margins. | print | `PageDescriptor.Foreground()` |
| `DocumentInfo` | class | Title, author, subject, keywords, language, creator, producer, dates. Named for the PDF *document information dictionary*. | PDF | `DocumentMetadata` |
| `Document.Info` | property | The document's `DocumentInfo`. | PDF | `Document.Metadata` |
| `ExportPdf()` | method | Writes the document as PDF, to bytes, a stream or a file. *Export* is what InDesign calls it. | InDesign | `GeneratePdf` |
| `ExportPdfAndOpen()` | method | Writes the document to a temporary PDF and opens it in the system's viewer. | plain | `GeneratePdfAndShow` |
| `PdfExportOptions` | class | Options for export: the `Typefaces` to set text in, whether to `Compress` streams, whether to `RequireEveryGlyph`, and whether to `KeepFontHinting`. | InDesign | `PdfGenerationOptions` |

## Frames and composing into them

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `IFrame` | interface | A place content is set into. Every method below that takes one returns another, so modifiers chain until content ends the chain. | InDesign | `IContainer` |
| `ISnippet` | interface | A reusable piece of composition, `Compose(IFrame)`. InDesign calls reusable content *snippets*. | InDesign | `IComponent` |
| `Snippet(ISnippet)` / `Snippet<T>()` | method | Places a snippet. | InDesign | `Component` |
| `Compose(Action<IFrame>)` | method | Composes into a frame with a method of your own. | print | `Element` |
| `ComposePerPage<TState>(IDynamicContent<TState>)` | method | Composes content afresh for every page it reaches, from the state it got to. | plain | `Dynamic` |
| `IDynamicContent<TState>` | interface | Content composed page by page: an `Initial` state, and `Compose(DynamicPage, TState)`. | plain | `IDynamicComponent` |
| `DynamicPart<TState>` | record | What dynamic content draws on a page: its `Content`, the `Next` state, and whether it `HasMore`. | plain | `DynamicComponentComposeResult` |
| `DynamicPage` | class | The page dynamic content is composed for: `Facts`, `Room`, `ReadingDirection`, `DefaultType`, `Measure` and `PositionsOf`. | plain | `DynamicContext` |
| `CapturePosition(string)` | method | Records where content is drawn, page by page, for dynamic content to look up. | plain | `CaptureContentPosition` |
| `CapturedPosition` | struct | Where captured content was drawn: its `Folio`, `Position` and `Size`. | plain | `PageElementLocation` |
| `ComposeLater(Action<IFrame>, bool keep)` | method | Composes only when layout reaches the frame, letting the content go once drawn unless kept. | plain | `Lazy`, `LazyWithCache` |
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
| `FlowColumns(Action<FlowColumnsComposer>)` | method | Columns a story flows through as a newspaper's does, on to the next page when full. | print ("threaded frames") | `MultiColumn` |
| `FlowColumnsComposer` | class | Builds flowing columns: `Columns`, `Gutter`, `Balanced`, `Story` for the content that flows, and `Between` for what is drawn in each gutter. *Story* is InDesign's word for text that flows from frame to frame. | print | `MultiColumnDescriptor` |
| `Flow(Action<FlowComposer>)` | method | Items set side by side as words are, wrapping on to new lines. | print ("inline") | `Inlined` |
| `FlowComposer` | class | Builds a flow: `Gutter`, `SpaceBetweenLines`, `FlushLeft`, `Centered`, `FlushRight`, `Justified`, `SpacedAround`, `FlushTop`, `Middle`, `FlushBottom`, and `Add` for each item. | print | `InlinedDescriptor` |
| `Grid(Action<GridComposer>)` | method | Cells flowing into rows of equal columns. | print ("layout grid") | `Grid` |
| `GridComposer` | class | Builds a grid: `Columns`, `Gutter`, `SpaceBetweenRows`, `FlushLeft`, `Centered`, `FlushRight`, and `Cell(span)` for each cell. | print | `GridDescriptor` |
| `ColumnsComposer.Share(float)` | method | A column taking a share of the width left over, in proportion to its weight. | plain | `RowDescriptor.RelativeItem` |
| `ColumnsComposer.Fixed(float)` | method | A column of a fixed width. | plain | `RowDescriptor.ConstantItem` |
| `ColumnsComposer.Natural()` | method | A column as wide as its content. | plain | `RowDescriptor.AutoItem` |
| `ColumnsComposer.Gutter(float)` | method | Space between columns. | print | `RowDescriptor.Spacing` |
| `Table(Action<TableComposer>)` | method | Rows and columns of cells, paginated at row boundaries. | print | `Table` |
| `TableComposer.Columns(Action<TableColumns>)` | method | Defines the table's columns. | print | `ColumnsDefinition` |
| `TableColumns.Fixed(float)` / `Share(float)` | method | A fixed-width or proportional column. | plain | `ConstantColumn` / `RelativeColumn` |
| `TableComposer.Cell()` | method | The next cell, placed automatically or explicitly. | print | `Cell` |
| `TableComposer.HeaderRows(...)` / `FooterRows(...)` | method | Rows repeated at the top or bottom of every page the table spans. | InDesign | `Header` / `Footer` |
| `TableComposer.ExtendLastCellsToBottom()` | method | Stretches the last cell of every column to the bottom of the table on each page. | plain | `ExtendLastCellsToTableBottom` |
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
| `Fill(Ink)`, `Fill(Gradient)` | method | Paints the frame's whole area, in an ink or a gradient. | InDesign | `Background`, `BackgroundLinearGradient` |
| `Stroke(float)`, `StrokeLeft/Top/Right/Bottom` | method | A line around the frame's edge, of a given weight. | InDesign | `Border*` |
| `StrokeInk(Ink)`, `StrokeInk(Gradient)` | method | The ink a stroke is drawn in, or a gradient laid across all it covers. | InDesign | `BorderColor`, `BorderLinearGradient` |
| `DropShadow(Shadow)`, `DropShadow(Ink, blur, offsetX, offsetY, spread)` | method | Casts a soft shadow from the frame onto what lies beneath it. | InDesign ("drop shadow") | `Shadow` |
| `RoundCorners(float)`, `RoundCorners(topLeft, topRight, bottomRight, bottomLeft)` | method | Rounds the frame's corners, alike or each on its own; after `DropShadow`, the shadow's. | InDesign ("corner options") | `CornerRadius*` |
| `AlignStroke(StrokeAlignment)` | method | Whether the stroke just set lies inside the frame's edge, centred on it or outside it. | InDesign ("align stroke") | `BorderAlignment*` |
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
| `FitToContent()`, `FitWidthToContent()`, `FitHeightToContent()` | method | Gives what follows only the content's own size, so a fill or stroke hugs it. | InDesign ("fit frame to content") | `Shrink`, `ShrinkHorizontal`, `ShrinkVertical` |
| `TurnLeft()`, `TurnRight()` | method | A quarter turn. | plain | `RotateLeft/Right` |
| `DrawOrder(int)` | method | Content of a higher order is drawn over content of a lower one wherever it sits on the page. | InDesign ("arrange") | `ZIndex` |
| `Rotate(float)` | method | Any angle, clockwise about the frame's centre, leaving layout alone. | plain | same |
| `MirrorHorizontal()`, `MirrorVertical()`, `MirrorBoth()` | method | Reflects content. | print | `FlipHorizontal/Vertical/Over` |
| `LeftToRight()`, `RightToLeft()`, `Reading(ReadingDirection)` | method | Reading direction for the frame and its content. | print | `ContentFrom` |
| `DefaultType(Func<TypeStyle, TypeStyle>)` | method | Refines the type style content inherits. | print | `DefaultTextStyle` |

## Flow across pages

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `NewPage()` | method | Starts the next page. | print ("start on next page") | `PageBreak` |
| `KeepTogether()` | method | Never splits the frame across pages. | print | `ShowEntire` |
| `KeepTogetherWherePossible()` | method | Moves the frame whole to the next page when it would fit there, and splits it only when it is longer than a page. | print ("keep options") | `PreventPageBreak` |
| `RequireSpace(float)` | method | Starts a new page unless at least this much space remains. | plain | `EnsureSpace` |
| `When(bool)` | method | Includes the frame only when the condition holds. | plain | `ShowIf` |
| `When(Func<PageFacts, bool>)` | method | Includes the frame only on the pages the condition accepts. | plain | `ShowIf(Predicate<ShowIfContext>)` |
| `PageFacts` | struct | The page being laid out: its `Folio`, the `PageCount` once known, `IsFirst`, `IsLast`, `IsOdd`. | print | `ShowIfContext` |
| `RepeatOnEachPage()` | method | Drawn afresh on every page its container continues onto. | plain | `Repeat` |
| `DiscardOverset()` | method | Keeps what fits where the frame first appears and discards the rest. *Overset* is the typesetter's word for content that does not fit. | InDesign ("overset text") | `StopPaging` |
| `Once()` | method | Drawn only the first time, even in a repeating band. | plain | `ShowOnce` |
| `SkipFirst()` | method | Drawn every time but the first — "continued" labels. | plain | `SkipOnce` |

## Rules, links and placeholders

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Rule(float, Ink?, StrokeStyle)`, `Rule(float, Ink, IReadOnlyList<float>)` | method | A horizontal line, solid, in a stroke style, or in dashes and gaps of the lengths given; each also takes a `Gradient` in place of the ink. | print | `LineHorizontal`, `LineDashPattern` |
| `VerticalRule(float, Ink?, StrokeStyle)`, `VerticalRule(float, Ink, IReadOnlyList<float>)` | method | A vertical line — between columns, say — styled as a rule is. | print | `LineVertical` |
| `Link(string url)` | method | Makes the frame a link to a URL. | plain | `Hyperlink` |
| `Anchor(string name)` | method | Names a place others can refer to. | print, InDesign ("text anchor") | `Section` |
| `Bookmark(string, int)` | method | An entry in the document's outline, nested by level, leading to where the content starts. | print ("bookmark") | outline from `Section` |
| `CrossReference(string anchor)` | method | Makes the frame a link to an anchor. | print | `SectionLink` |
| `Placeholder(Ink?)`, `Placeholder(string, Ink?)` | method | A box standing in for content not there yet, saying what will go there if given words. | print | `Placeholder` |
| `SampleData` | class | Stand-in content from a seed, the same every time: dummy text (`Words`, `Heading`, `Sentence`, `Query`, `Paragraph`, `Paragraphs`), `PersonName`, `EmailAddress`, `WebAddress`, `TelephoneNumber`, `Number`, `DecimalNumber`, `Percentage`, `Amount`, `TimeOfDay`, `Date`, `WrittenDate`, `Timestamp`, `Ink`, `PaleInk` and `Image`. | print ("dummy text") | `Placeholders` |
| `Image(IImage, ImageFitting)` | method | Places an image. | plain | `Image` |
| `Image(Func<ImageRequest, byte[]?>)` | method | An image generated for the box it fills, at the resolution images are generated at. | plain | `Image(GenerateDynamicImageDelegate)` |
| `ImageRequest` | struct | What a generated image is asked for: its `Size`, `PixelWidth`, `PixelHeight` and `Resolution`. | plain | `GenerateDynamicImageDelegatePayload` |
| `Artwork(Func<Extent, Artwork?>)` | method | Artwork generated for the box it fills — an SVG written for that size, say. | plain | `Svg(Func<Size, string>)` |
| `PdfExportOptions.ImageResolution` | property | The resolution generated images are asked for, 288 pixels an inch unless set. | plain | `Settings.ImageRasterDpi` |
| `Artwork(Artwork, ImageFitting)` | method | Places vector artwork, fitted as an image is and kept vector in the PDF. | print ("artwork") | `Svg`, `Canvas` |
| `Artwork` | class | Vector artwork of a size of its own, made by `Draw(width, height, ...)` or read by `FromSvg` and `FromSvgFile`. | print | `SvgImage`, `DrawOnCanvas` |
| `TextAnchor` | enum | `Start`, `Middle`, `End`: which part of a line of artwork text sits at its point. | SVG | `text-anchor` |
| `ArtworkComposer` | class | Draws artwork: `Fill`, `Stroke`, `Clip`, `Text`, `Image`, `SaveState`, `RestoreState`, `Translate`, `Scale`, `Rotate`, `Transform`. | print | a Skia canvas |
| `VectorPath` | class | Straight and curved segments — `MoveTo`, `LineTo`, `CurveTo`, `QuadraticTo`, `ArcTo`, `Close` — and shapes: `AddRectangle`, `AddRoundedRectangle`, `AddEllipse`, `AddCircle`. | plain | `SKPath` |
| `FillRule` | enum | `NonZero`, `EvenOdd`: what counts as inside a path that crosses itself. | plain | `SKPathFillType` |
| `LineStyle` | struct | A stroke's `Weight`, `Cap`, `Join`, `MiterLimit`, `Dashes` and `DashOffset`. | plain | `SKPaint` stroke settings |
| `LineCap`, `LineJoin` | enum | How a stroke's ends (`Butt`, `Round`, `Square`) and corners (`Miter`, `Round`, `Bevel`) are finished. | plain | `SKStrokeCap`, `SKStrokeJoin` |
| `IImage` | interface | An image a frame can place, with its size in pixels the right way up. | plain | `IImage` |
| `RasterImage` | class | A JPEG or PNG, loaded with `FromBytes`, `FromStream` or `FromFile` and embedded as it was encoded wherever PDF allows. *Raster*, as prepress distinguishes pixel images from vector art. | print | `Image` |
| `RasterImage.WithQuality(int)`, `WithMaximumResolution(float)` | method | The image, recompressed at a quality or scaled to the resolution it is shown at when embedded. | plain | `WithCompressionQuality`, `WithRasterDpi` |
| `IImageProcessor` | interface | Re-encodes images for embedding when a quality or maximum resolution asks for it. | plain | — |
| `ImageProcessing` | struct | What a processor is asked: the `Source`, the `PixelWidth` and `PixelHeight` to make, and the `Quality`. | plain | — |
| `PdfExportOptions.ImageQuality`, `MaximumImageResolution`, `ImageProcessor` | property | Document-wide image quality and resolution, and what processes images to meet them. | plain | `Settings.ImageCompressionQuality`, `ImageRasterDpi` |
| `ImageFitting` | enum | `FitWidth`, `FitHeight`, `Proportionally`, `Stretch`, after InDesign's fitting options. | InDesign | `ImageFit` |

## Existing files

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `PdfFile` | class | A PDF file being put together from others, in `Rustaveli.Pdf.Operations`: `Open`, then `KeepPages`, `Append`, `Overlay`, `Underlay`, then `Save` or `ToArray`. | plain | `DocumentOperation` |
| `PdfFile.KeepPages(string)` | method | Keeps the pages a list such as `"1-3, 5, 8-last"` names, in its order. | print dialogs ("pages") | `TakePages` |
| `PdfFile.Append(...)` | method | Adds another file's pages, all or some, after these. | plain | `MergeFile` |
| `PdfFile.Attach(FileAttachment)` | method | Carries a file inside the PDF, listed among its attachments and associated with it for PDF/A-3. | plain ("attach") | `AddAttachment` |
| `FileAttachment` | class | A file to attach: its `Name` and `Content`, `MediaType`, `Description`, dates and `Relationship`; `FromFile` reads one from disk. | plain | `DocumentAttachment` |
| `AttachmentRelationship` | enum | How an attachment relates to the document: `Unspecified`, `Source`, `Data`, `Alternative`, `Supplement`. | ISO 32000-2 | `DocumentAttachmentRelationship` |
| `PdfFile.AddMetadata(string)` | method | Adds XMP descriptions — an electronic invoice's, say — to the file's metadata. | plain | `ExtendMetadata` |
| `PdfFile.Open(..., string? password)` | method | Opens a file, protected ones with the owner's or the user's password. | plain | `LoadFile(path, password)` |
| `PdfFile.Protect(Protection)`, `Unprotect()` | method | Saves the file protected anew, or unprotected; otherwise it keeps the protection it had. | Acrobat ("protect") | `Encrypt`, `Decrypt` |
| `PdfFile.OptimizeForWeb()` | method | Saves the file linearised, for viewing as it downloads. | Acrobat ("fast web view") | `Linearize` |
| `PdfFile.LiftRestrictions()` | method | Drops the restrictions a signature places on the file. | plain | `RemoveRestrictions` |
| `Protection` | class | Password protection: `UserPassword`, `OwnerPassword`, `Encryption`, and what a reader may do — `AllowPrinting`, `AllowCopying` and the rest. Also `PdfExportOptions.Protection`. | Acrobat ("password security") | `Encryption40Bit`, `Encryption128Bit`, `Encryption256Bit` |
| `EncryptionLevel` | enum | `Rc4With40Bits`, `Rc4With128Bits`, `AesWith128Bits`, `AesWith256Bits`. | Acrobat ("encryption level") | the `Encryption*` classes |
| `IncorrectPasswordException` | class | A protected file opened without its password, or with a wrong one. | plain | qpdf's errors |
| `PdfFile.Overlay(...)`, `Underlay(...)` | method | Draws another file's pages over these, as a stamp, or beneath them, as a letterhead, in turn onto the pages named. | print ("overlay", "letterhead") | `OverlayFile`, `UnderlayFile`, `LayerConfiguration` |

## Structure and standards

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `ContentTag` | class | The part content plays in a tagged document, named in words and written as PDF's standard structure types: `Section`, `Article`, `Division`, `BlockQuote`, `Caption`, `Index`, `Contents`, `ContentsEntry`, `Paragraph`, `List`, `ListItem`, `ListLabel`, `ListBody`, `Table`, `Quote`, `Code`, `Note`, `Span`, and made by `Heading(int)`, `Abbreviation(string)`, `Figure(string)`, `Formula(string)`. | ISO 32000 ("standard structure types"), InDesign ("tags") | `SemanticSection`, `SemanticHeader1`–`6`, `SemanticFigure`, … |
| `Tagged(ContentTag)` | method | Tags a frame's content with the part it plays. Text nothing else tags is a paragraph; lists and links are tagged without asking. | InDesign ("tag") | `Semantic…` |
| `Untagged()` | method | Leaves a frame's content out of the structure, as decoration no screen reader reads. | print ("artifact") | `SemanticIgnore` |
| `Language(string)` | method | The language a frame's content is in, where it differs from the document's. | plain | `SemanticLanguage` |
| `CellFrame.RowHeading()` | method | Makes a cell the heading of its row in a tagged table, as header rows head their columns. | plain | `AsSemanticHorizontalHeader` |
| `PdfExportOptions.Tagged` | property | Whether the PDF records the document's structure and reading order. | ISO 32000 ("tagged PDF") | implied by `PDFUA_Conformance` |
| `PdfUAConformance` | enum | The part of PDF/UA a document claims: `None` or `PdfUA1`. | ISO 14289 | `PDFUA_Conformance` |
| `PdfExportOptions.Accessibility` | property | The PDF/UA part to claim: tagged, titled, in a named language, every glyph found. | ISO 14289 | `Settings.PDFUA_Conformance` |
| `PdfAConformance` | enum | The part and level of PDF/A a document is written to: `None`, `PdfA2B`, `PdfA2U`, `PdfA2A`, `PdfA3B`, `PdfA3U` or `PdfA3A`. | ISO 19005 | `PDFA_Conformance` |
| `PdfExportOptions.Conformance` | property | The PDF/A part and level to write: XMP metadata, an sRGB output intent, inks in RGB, every glyph found, and at the `A` levels the structure. | ISO 19005 | `Settings.PDFA_Conformance` |

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
| `TypeStyle.WithTypeface`, `WithPointSize`, `WithInk`, … | method | A copy with one attribute changed; `WithTypeface` also names the typefaces to fall back to, in order. | plain | `FontFamilyOf`, `FontSizeOf`, `ColorOf`, … |
| `TypeStyle.WithFeature(string, int)` | method | Turns an OpenType feature on, off or to an alternate by its four-letter tag; `Ligatures`, `SmallCapitals`, `OldstyleFigures` and `TabularFigures` name the common ones. | print (OpenType) | `EnableFontFeature` |
| `Typeface` | term | A font family, such as Noto Sans. | print | `FontFamily` |
| `PointSize` | term | Type size in points. | print | `FontSize` |
| `Leading` | term | Line spacing, as a multiple of the point size. | print | `LineHeight` |
| `Tracking` | term | Uniform extra space between letters. | print | `LetterSpacing` |
| `Highlight` | term | A colour behind a run of text. | plain | `BackgroundColor` |
| `TypeWeight` | enum | `Thin` … `Black`, `ExtraBlack`. | print | `FontWeight` |
| `ScriptPosition` | enum | `Normal`, `Subscript`, `Superscript`. | print | `FontPosition` |
| `StrokeStyle` | enum | `Solid`, `Double`, `Dotted`, `Dashed`, `Wavy`: how a text stroke (underline, strike-through, overline) or a rule is drawn. | print | `TextStyle.Decoration*` |

## Colour

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `Shadow` | struct | A shadow's `Ink`, `Blur`, `Offset` and `Spread`, in points, as CSS measures a box shadow. | InDesign ("drop shadow") | `BoxShadowStyle` |
| `GradientStop` | struct | An ink at a position along a gradient, from 0 to 1. | InDesign ("gradient stop") | — |
| `Gradient` | class | A linear blend of inks at an angle, clockwise from left to right: `Across`, `Down`, or any angle. | InDesign ("gradient swatch") | `BackgroundLinearGradient`'s arguments |
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
| `Corners` | struct | Four radii, one per corner, clockwise from the top left. | plain | — |
| `StrokeAlignment` | enum | `Inside`, `Center`, `Outside`: where a stroke lies against the edge. | InDesign | `BorderAlignment*` |
| `PaperSizes` | class | ISO A, B and C series, US and architectural sizes, envelopes. | print | `PageSizes` |
| `Landscape()` / `Portrait()` | method | An extent turned to its wide or tall orientation. | print | same |
| `LengthUnit` | enum | `Point`, `Millimetre`, `Centimetre`, `Metre`, `Inch`, `Foot`, `Mil`, `Pica`. | plain | `Unit` |
| `Lengths` | class | Conversions such as `20.Millimetres()`. | plain | `UnitExtensions` |
| `ReadingDirection` | enum | `LeftToRight`, `RightToLeft`. | print | `ContentDirection` |

## Typefaces and export

PDF is written in managed code ([ADR 0001](adr/0001-managed-pdf-writer.md)): fonts are subset to the glyphs a
document uses, and images are embedded as they were encoded wherever PDF can carry them.

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `TypefaceLibrary` | class | The typefaces documents are set in: those registered with it, then those installed. `Shared` serves any export given no library of its own. | print ("type library") | `FontManager`, `SkiaFontProvider` |
| `TypefaceLibrary.Register(...)` / `RegisterFile(...)` / `RegisterResource(...)` | method | Adds every face in a font file, stream or embedded resource, under its own names and optionally a typeface name given. A registered typeface shadows an installed one of the same name. | plain | `FontManager.RegisterFont`, `RegisterFontWithCustomName`, `RegisterFontFromEmbeddedResource` |
| `TypefaceLibrary.Fallbacks` | property | Typefaces tried in order for a character a run's own typeface lacks. | plain | `FallbackFamilies` |
| `TypefaceLibrary.SearchFolder(string)` | method | Adds a folder searched for typefaces as installed ones are, after the registered ones. | plain | `Settings.FontDiscoveryPaths` |
| `PdfExport` | class | The `ExportPdf` methods, to bytes, a stream or a file. | InDesign | `PdfGenerationExtensions` |
| `ImageExport` | class | The `ExportImages` methods of the `Rustaveli.Pdf.Raster` package: every page as an image, drawn by SkiaSharp from the same layout and glyphs as the PDF. | InDesign ("Export JPEG") | `GenerateImages` |
| `ComplexScripts` | class | `ShapeComplexScripts`, from the `Rustaveli.Pdf.Shaping` package: shapes Arabic, Hebrew points, Indic and South-East Asian scripts with HarfBuzz for the text a `TypefaceLibrary` sets. | typesetting ("complex scripts") | built in |
| `ImageExportOptions` | class | The `Resolution` in pixels per inch, the `Format`, the `Quality` of lossy formats, the `Typefaces`, and whether to `RequireEveryGlyph`. | InDesign | `ImageGenerationSettings` |
| `SkiaImageProcessor` | class | Scales and recompresses images for PDF export with Skia, turning them the right way up. | plain | — |
| `ExportSvg()` | method | Writes every page as an SVG document, text as outlines and images within. | plain | `GenerateSvg` |
| `ExportXps()` | method | Writes the document as one XPS document; Windows only. | plain | `GenerateXps` |
| `VectorExportOptions` | class | For SVG and XPS: the `Typefaces`, whether to `RequireEveryGlyph`, and the `ImageResolution` generated images are asked for. | plain | `ImageGenerationSettings` |
| `SvgExport`, `XpsExport` | class | Hold `ExportSvg` and `ExportXps`. | plain | — |
| `PageImageFormat` | enum | `Png`, `Jpeg`, `Webp`. | plain | `ImageFormat` |

## Failures

| Name | Kind | Meaning | Source | Replaces |
|---|---|---|---|---|
| `TypesettingException` | class | Base of every failure raised while typesetting. | print | — |
| `OversetException` | class | Content that cannot fit, even on an empty page. In print, text that does not fit its frame is *overset*. | InDesign | `DocumentLayoutException` |
| `MissingGlyphException` | class | Characters no typeface has, when an export requires every glyph; `Characters` lists them. A layout application flags a *missing glyph* rather than let it go to press. | InDesign | `Settings.CheckIfAllTextGlyphsAreAvailable` |
| `RenderingException` | class | A failure while drawing a page, carrying the page number. | plain | `DocumentDrawingException` |
| `CompositionException` | class | A failure while composing the document, before layout. | print | `DocumentComposeException` |
| `UnreadableFileException` | class | A file that cannot be read as a PDF: not one, or damaged past repair. | plain | qpdf's errors |

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
