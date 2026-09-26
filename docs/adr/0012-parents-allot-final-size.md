# 0012 — Parents allot the final size; decorators fill it

**Status:** Accepted

## Context
The first visual snapshots of the conformance specimens showed table stripes and header fills stopping at the end
of their text, borders hugging words inside wider row items, layers aligned to the bottom of a tall area drawn on
its first line, and right-to-left rows whose boxes and labels drifted apart.

All of it came from one ambiguity in the layout contract. `Draw(availableSpace)` was treated as "the space on
offer", so a parent could pass more than a child would use — a column handed each item everything left on the
page — and decorators protected themselves by re-measuring their child and painting its natural size. The two
halves were each reasonable and together wrong: a background could never be wider than its text.

## Decision
`Draw` receives the **final size** its parent allots, and an element occupies exactly that size.

- A parent measures first, then decides each child's final size and passes it: a column gives an item the full
  width and the item's measured height; a row gives each item its width and the row's height; a table gives a
  cell its span's width and height; layers give every layer the full area; a decoration gives its content the
  content's measured height.
- Decorators — background, border, link areas, layers — paint the size they are given. They no longer re-measure
  to decide how much to paint.
- The allotted size is never smaller than what the child measured for the same width, so drawing within it
  produces the same content the measurement promised.

This is the behaviour of block boxes in CSS, and what a user expects of `Background` in a table cell.

## Consequences
- Zebra-striped tables, bordered row items and right-to-left rows render as intended.
- Visual snapshots can be approved: they now pin correct output.
- A decorator placed directly on the page content fills the content area, as it would in any box model. Wrapping
  it in an alignment or a size constraint makes it hug its content instead.
- Tests that asserted a decorator's painted size equals its child's natural size change their expectation.
