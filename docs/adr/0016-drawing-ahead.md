# 0016 — Layout may draw ahead, and return

**Status:** Accepted

## Context
Measuring must not change anything: the engine measures speculatively, often more than once, and discards what it
does not use. Most layout honours that by asking each child once, but some layout can only know where content ends
by laying it out. A story flowing through columns does not know where the second column starts until the first has
been drawn; balancing those columns means trying one height after another. Content composed page by page has the
same need in another form: it must say what it would draw without having drawn it.

Elements keep their progress in their own fields — how many lines of a paragraph are drawn, how many rows of a
table, which items of a stack — and `ResetOwnState` already clears it for a new pass.

## Decision
Every element that remembers progress can also **save and restore** it: `SaveOwnProgress` returns a copy of what
`ResetOwnState` would clear, and `RestoreOwnProgress` puts it back. `Block.SaveProgress` gathers the copies of a
whole tree and `RestoreProgress` returns every element to them.

Layout that must see ahead saves the progress of its content, draws it onto a surface that keeps nothing, notes
where it ended, and restores it before returning — so measuring still changes nothing.

Content composed page by page does not keep progress in fields at all: each composition is handed the state it got
to and returns the state after it, and only drawing advances.

## Consequences
- Flowing columns and balancing are exact: they use the same layout that draws, not an estimate.
- Every new stateful element must override the pair alongside `ResetOwnState`; a round-trip test over every kind of
  stateful content guards them.
- Drawing ahead costs a layout per trial. Balancing narrows in by halves, a fixed number of trials a page.
