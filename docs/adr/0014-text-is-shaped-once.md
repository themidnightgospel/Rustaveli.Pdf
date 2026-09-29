# 0014 — Text is shaped once, in the core, for measuring and drawing alike

**Status:** Accepted

## Context
Layout measures text long before anything is drawn, and draws it once the page is settled. With Skia, measuring
and drawing went through the same `SKFont`, so the two agreed by construction — but Skia chose the fallback faces,
applied no kerning, and could not tell the PDF writer which glyph it had used for which character.

The managed writer ([ADR 0001](0001-managed-pdf-writer.md)) needs exactly that: glyph indices to subset and to
show, the characters behind them for a ToUnicode map, and advances that match what layout reserved. Any second
opinion about which face sets a character, or how wide it is, puts a glyph in one place and its space in another.

## Decision
Text is shaped in one place. A `TypeShaper` resolves a type style to a face — the registered or installed face
nearest its weight and slant, else a common substitute for its kind, else any registered face — and walks text as
glyphs, choosing a fallback face for each character the face lacks. The `GlyphWalk` it returns is the only way text
is measured, fitted or drawn:

- the measurer sums the walk's advances, pair kerning and tracking;
- fitting stops the same sum early, so a prefix it accepts measures within the width;
- the PDF surface shows the walk's glyphs, positioned by the font's widths, with kerning as text-array adjustments
  and tracking as character spacing.

Kerning from the fonts' `GPOS` and `kern` tables is on. Faces are embedded as Type 0 fonts over CID fonts: TrueType
subset to the glyphs used, numbered in order of first use; CFF whole. Every glyph remembers the character it was
first used for, and the ToUnicode map records it.

Typefaces are registered with a `TypefaceLibrary`. A registered typeface shadows an installed one of the same name,
so a document whose typefaces are registered comes out the same everywhere; `Fallbacks` names the typefaces tried
first for a missing character. A typeface nobody has is substituted, as a desktop publisher would, rather than
refusing the document; only when no face at all is available does export fail.

## Consequences
- Measuring and drawing cannot disagree, and kerned text now matches what print tools and QuestPDF set.
- Every future backend — page images through Skia, or anything else — draws from the same walk and inherits the
  same layout, instead of shaping text its own way.
- The walk is where the text engine grows ([phase 3](../about.md#history)): ligatures and other `GSUB` features,
  mark positioning, bidirectional reordering and complex scripts change what the walk yields, not its callers.
- Export streams pages out as they finish, so a failed export to a caller's stream leaves part of a document there;
  export to a path writes beside the target and moves into place.
