# 0012 — Parents allot the final size; decorators fill it

**Status:** Accepted

## Context
The first visual snapshots of the conformance specimens showed table stripes and header fills stopping at the end
of their text, strokes hugging words inside wider columns, layers aligned to the bottom of a tall area drawn on
its first line, and right-to-left columns whose boxes and labels drifted apart.

All of it came from one ambiguity in the layout contract. `Render(availableSpace)` was treated as "the room on
offer", so a parent could pass more than a child would use — a stack handed each item everything left on the
page — and decorators protected themselves by planning their child again and painting its natural size. The two
halves were each reasonable and together wrong: a fill could never be wider than its text.

## Decision
`Render` receives the **final size** its parent allots, and a block occupies exactly that size.

- A parent plans first, then decides each child's final size and passes it: a stack gives an item the full
  width and the item's planned height; columns give each column its width and the row's height; a table gives a
  cell its span's width and height; layers give every layer the full area; bands give the body its planned height.
- Decorators — fills, strokes, link areas, layers — paint the size they are given. They no longer plan again to
  decide how much to paint.
- The allotted size is never smaller than what the child planned for the same width, so drawing within it
  produces the same content the plan promised.

This is the behaviour of block boxes in CSS, and what a user expects of `Fill` in a table cell.

## Consequences
- Zebra-striped tables, stroked columns and right-to-left columns render as intended.
- Visual snapshots can be approved: they now pin correct output.
- A decorator placed directly in a section's body fills the body, as it would in any box model. Placing it inside a
  placement or a size constraint makes it hug its content instead.
- Tests that asserted a decorator's painted size equals its child's natural size change their expectation.
