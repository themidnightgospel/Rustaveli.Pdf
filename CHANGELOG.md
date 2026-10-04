# Changelog

Versions follow [semantic versioning](https://semver.org/). Before 1.0 the public surface may still change from one
minor version to the next; from 1.0, nothing public changes incompatibly within a major version.

## Unreleased

**Fixed**

- Reading a PDF: an object stream whose `/First`, or an offset in its header, placed an object before the start of
  its data threw an `IndexOutOfRangeException`, and one beyond 32 bits could read the wrong bytes as the object.
  Such an object is now treated as one not where it is listed: the file is repaired, and the object reads as null.
- Reading a PDF: a stream whose filter array held something other than a name threw an `InvalidOperationException`
  and failed the whole file; it is damage now, and an object stream so damaged holds nothing when the file is
  repaired.
- Reading a PDF: a `/Crypt` filter naming its crypt filter with something other than a name threw an
  `InvalidOperationException`; it is damage now, an `UnreadableFileException`.
- Repairing a PDF: one object too damaged to read made the whole file unreadable. It is left out now, and the rest of
  the file is read; the damaged object is still refused when it is asked for.
- An encrypted file with a cross-reference stream kept its catalog in an object stream. That is allowed, but readers
  that look the catalog up before setting up decryption, PdfPig among them, could not open the file. The catalog of an
  encrypted file is now written outside the object streams, as qpdf writes it.
- Content composed per page (`ComposePerPage`) could lose the last of itself. Planned in the room left on a page, it
  ended there; drawn in the smaller box its parent allotted, it was composed again, fitted less and went on — after
  the parent had moved past it, so what it kept for the next page was never drawn. It is now drawn as it was planned.
- Page images drew a fill thinner than a pixel, such as a 0.25-point rule, as a faint smear across two pixels. It is
  drawn one pixel thick, on the pixel its middle falls in, as PDF viewers draw it. SVG and XPS keep it as thin as it is.
- A family with both a CFF2 face and a TrueType face, as a variable font installed in both builds has, could be
  matched to the CFF2 face, which cannot be embedded, so its text was set in a substitute. A face that can be embedded
  is now chosen over a closer one that cannot.
- Page images on macOS failed for text set in any face of a collection but its first, which Skia's font manager there
  does not load — among them fallback faces macOS installs in collections, such as Apple SD Gothic Neo and Apple
  Color Emoji. The face is now given to Skia as a font of its own, on every platform.
- `Artwork.FromSvg(Stream)` on .NET refused a document declaring a code page such as Windows-1251 or Shift JIS, as
  XML that was not well-formed; it is read in the encoding it declares, as on .NET Framework.

**Performance**

- Each word is shaped once per export when text is measured, however often it recurs and however many times layout
  measures it: an 85-page report is laid out in about half the time.
- Paragraphs no longer allocate an object and a string for every word and every space. A paragraph's lines are
  built in pieces lent from one paragraph to the next, and each line keeps only the pieces it is drawn in; words are
  measured where they lie in their text. The 85-page report allocates a quarter of what it did, and every document's
  PDF is unchanged, byte for byte.
- A paragraph without inline frames no longer allocates a list of its children every time layout starts a pass. The
  10,000-row table allocates 9% less, the report and the invoice about 6% less; every PDF is unchanged.
- A paragraph keeps the lines of the last two widths it was set at, not one. Text set in a box of its own natural
  width, such as flush-right text in a table cell, is planned at one width and drawn at another on every pass, and
  built its lines afresh each time. The 10,000-row table allocates a quarter less and lays out about 15% faster, the
  invoice allocates 13% less; every PDF is unchanged.
- `Text("…")` sets its paragraph directly instead of composing it through a handler, so a plain paragraph or table
  cell no longer allocates a delegate and two composers, and keeps room for exactly its one run. The 10,000-row table
  allocates 9% less, the invoice 5% and the report 3% less; every PDF is unchanged.

**Tests**

- Existing files are now read as other writers lay them out: qpdf rewrites every specimen with object streams on and
  off, streams uncompressed or compressed again, linearised, and encrypted with 40-bit RC4, 128-bit RC4 and AES, and
  256-bit AES. Each is read page by page, saved, joined to a document of ours, and checked by qpdf.
- Every package is held to the coverage gate. Operations, Shaping and Preview were outside it; new tests of the
  reader, the assembler, the stream filters and the linearizer bring them in at 99.6% line and 98.8% branch coverage
  overall.
- A new specimen sets content no export had set before — composed per page, composed late, unbounded, placeholders, a
  turned frame and a proportion — so each is now validated, compared visually and drawn by both renderers.
- Fonts in the formats the bundled Noto files never exercise — a variable TrueType font, a CFF2 font, a collection
  and a font kerned only by a `kern` table — are registered, measured, set, embedded and drawn, and their subsets
  checked by qpdf and veraPDF.
- resvg's SVG test suite, 1,693 documents, is read and drawn: each as a page and an image at the size it gives
  itself, with its frame drawn, in a PDF qpdf finds sound.

## 0.2.0

Fixes from a review of the whole library, each found by a test that failed first. 0.1.0 and 0.1.1 are unlisted in
favour of this release.

**Fixed**

- Layout: content under `RequireSpace` inside a stack, row, table cell or content-sized page vanished; a natural-width
  row column measured where no room was left failed the export; `KeepTogetherWherePossible` inside an inset threw; a
  table cell that could not be set in its row was cut off silently; trial layouts of flowing columns recorded anchors
  and positions, so references could print the wrong page; flow items of no size never took effect; draw orders set by
  content composed later were ignored; counting stopped before anchors and positions had settled.
- Text: right-to-left brackets shaped by HarfBuzz faced the wrong way; invisible characters a face lacks (variation
  selectors, joiners, bidi marks, controls) were drawn as boxes; soft hyphens showed mid-line; a word could be split
  inside a character; fallback faces depended on which weight asked first and on what earlier documents found;
  tracking pulled marks off their letters and joined scripts apart; marks could be set in another face; `kern` turned
  off was ignored; lines were not tall enough for fallback faces; a linked span drawn in pieces was tagged as many
  links; HarfBuzz shaped in the machine's language, and scripts beyond the Basic Multilingual Plane were not shaped.
- Fonts: a GSUB lookup that could not be read was read again on every run and could, over a long process, stop every
  later lookup; CID-keyed CFF subsets could be scaled twice; unreadable kerning, cmap subtables and header-only glyphs
  failed exports; a font file unreadable at first was never read again; nested lookups ignored where they were called;
  `rclt` is now on by default; `dotsection` no longer stops CFF subsetting.
- PDF output: an image drawn after a translucent fill was drawn translucent; AES-256 in a PDF 1.7 file now declares
  Adobe extension level 8; a carriage return in document information made the XMP disagree with it.
- Existing files: links to named destinations in appended files led into the wrong file; hidden layers became visible,
  and form fields of appended files were lost; a page kept twice shared its annotations; objects of a later generation
  and files whose cross-reference was lost decrypted wrongly; attachment-only encryption was not understood, and
  attachments were written plain under an encryption dictionary that said otherwise; a file used as a layer could not
  be saved from several threads; overlays ignored page rotation and size; a stream could inflate without limit; stray
  bytes between JPEG segments were refused.
- SVG: percentages were taken of 100 px, not the viewport; `<tspan>` positions and styles were ignored; `<switch>` drew
  every child; font weights were only normal or bold; `FromSvg(Stream)` closed the caller's stream and ignored the
  declared encoding.
- Companion packages: the preview failed and kept its port where no browser could be opened; page images and
  reprocessed images ignored colour profiles; HarfBuzz fonts were made per shaper and could be made twice.

**Changed**

- Numbers are checked where they are given: frame modifiers (inset, stroke, sizes, proportion, shrink, scale, rotate,
  shift, require-space, rules), gutters and spacing, a section's trims (at most 14,400 pt) and margins, run refinements,
  and image resolutions, each with `ArgumentOutOfRangeException` or `ArgumentException` naming the argument.
- Composition mistakes are refused while composing: a table with cells but no columns, a row or cell span below one, a
  second story, between or base layer, linked text without a target.
- PDF/UA with accessibility withheld from a protected file, passwords RC4 and 128-bit AES would reduce to question
  marks, and document information holding characters XML cannot hold are refused.
- Saving a file whose attachments alone are encrypted, opened without its password, is refused where its attachments
  cannot be carried as they are.
- A page laid on another of a different size or turn is turned, centred and shrunk to fit it; laid on a tagged page it
  is an artifact.
- SVG lengths in unknown units are errors, as SVG says, and fall back.
- Reprocessed images are converted to sRGB.
- The companion packages depend on exactly the version of `Rustaveli.Pdf` they were built with.

**Performance**

- Plain documents are counted once instead of twice; a long table with `ExtendLastCellsToBottom` no longer takes
  quadratic time (48,000 cells: ~8 s to ~0.1 s); a centred table is laid out once per page, not three times; balanced
  flowing columns copy their story's progress once per measurement.
- A page's resources are named in linear time (50,000 images: 5.6 s to 54 ms); streams are encrypted without copies;
  measuring ligatures and shaped text allocates nothing once warm; a layer laid on many pages is written once; Skia
  loads each face once and reads transparency from the alpha plane.

**Packaging**

- Releases build and test with a read-only token; only a separate job that runs no repository code can publish.

## 0.1.1

Fixes found by fuzzing the readers of fonts, images, SVG and PDF files, which now runs on every change, and by the
issues left from 0.1.0.

**Packaging**

- Each package comes with a signed build provenance attestation:
  `gh attestation verify <package>.nupkg --repo themidnightgospel/Rustaveli.Pdf` shows it was built here, from this
  release's commit.

**Fixed**

- A table cell pinned to a row with no room left in it covered a cell already there; it is now refused with a
  `CompositionException` naming the row.
- Layout error messages, and `Extent` and `Offset` as text, wrote numbers in the current culture — `0,000` on a
  machine that writes decimals with a comma. They read the same in every culture now.
- A running head or foot of no height was not drawn at all, so an anchor, bookmark or marker in it never took effect
  and links to it went nowhere.
- Content that threw while it was measured escaped unwrapped, with nothing to say where in the document it was. It is
  wrapped in a `RenderingException` naming the page, as a failure while drawing is.
- SVG artwork with numbers beyond what a PDF can hold (10^15), or transforms, clips, gradients and view boxes that
  reach beyond it, failed the export; what reaches beyond it is left out. `ArtworkComposer` refuses such numbers
  when they are given.
- SVG path data with anything but a command after a close looped forever.
- An SVG opacity above 1 failed; opacities are clamped, as SVG says.
- Reading a PDF: a number with two decimal points, a cross-reference stream without a usable `/W` or `/Size`, an
  AES-256 file whose wrapped key is not 32 bytes, and an RC4 or AES-128 file with a key outside 40 to 128 bits or an
  owner entry shorter than 32 bytes threw exceptions `PdfFile.Open` does not document. The first two are repaired;
  the others are an `UnreadableFileException`.

**Changed**

- `PdfFile.Open` documents the `NotSupportedException` it throws for a security handler other than the standard one.
- A type style's point size, stroke weight and leading must be zero or more, and its tracking and word spacing
  numbers a PDF can write; each throws `ArgumentOutOfRangeException` otherwise — NaN, an infinity or a negative size
  used to be taken, and failed only when the page was drawn. A run's `PointSize`, `StrokeWeight`, `Leading`,
  `Tracking` and `WordSpacing` check where they are called. In SVG, a negative `font-size` is ignored, as SVG says.

## 0.1.0

The first release: a complete PDF generator in a vocabulary drawn from print — see the [parity checklist](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/docs/parity/PARITY.md) and
the [guides](https://github.com/themidnightgospel/Rustaveli.Pdf/blob/main/docs/guide/README.md).

**Packages**

- `Rustaveli.Pdf` — layout, text and PDF export, in managed code with no native dependencies.
- `Rustaveli.Pdf.Raster` — page images, SVG pages and XPS, and image recompression, through SkiaSharp.
- `Rustaveli.Pdf.Shaping` — Arabic, Hebrew points, and Indic and South-East Asian scripts, through HarfBuzz.
- `Rustaveli.Pdf.Operations` — reading and changing existing PDF files.
- `Rustaveli.Pdf.Preview` — a live preview in the browser, redrawn on hot reload, with an inspector.

Each targets .NET 10 and .NET Standard 2.0.

**Layout** — sections with running heads and feet, underlays and overlays; stacks, columns, tables with repeated
header and footer rows and spanning cells, lists, layers, bands, grids, flows and flowing columns; insets, fills,
strokes, gradients, rounded corners, drop shadows, size constraints, placement, rotation, scaling and mirroring;
keeping content together, conditions on the page, repetition, and content composed per page; named styles.

**Text** — styled runs, paragraphs aligned, justified and indented, lines broken by the Unicode rules, text in both
directions, folios, page counts and cross-references; typefaces registered or installed, fallback per character,
OpenType features and kerning, and Noto Sans carried in the package as the default typeface.

**Images and artwork** — JPEG and PNG embedded as encoded, SVG and drawn artwork kept vector, and images and
artwork made for the box they fill.

**Output** — PDF with TrueType and CFF fonts subset, their hinting left out unless kept, CMYK and spot inks, links and bookmarks; PDF/A-2 and PDF/A-3 at levels B, U and
A; tagged PDF and PDF/UA-1; password protection; page images, SVG and XPS. Pages render in parallel, and a document
can be exported from many threads at once.

**Existing files** — merging, reordering, stamping and letterheads, attachments for electronic invoices, metadata,
protection and linearisation.

**Tooling** — the live preview and its inspector, frame edges and names, and layout failures traced to the frame
that did not fit.
