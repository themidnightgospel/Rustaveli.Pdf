# 0017 — Draw order holds a page back

**Status:** Accepted

## Context
Content is drawn in the order it comes: whatever follows is painted over what came before. A frame that should lie
over its neighbours — a badge overlapping the next row, a highlight beneath a whole table — cannot be moved in the
tree without changing the layout.

## Decision
A frame can set a **draw order**. On pages whose sections use one, the surface holds every drawing back until the
page ends, each with the chain of transforms and clips it was made under, and then draws them lowest order first;
drawings of one order keep the order they were made in. The paper stays beneath everything.

The chain is shared: each transform or clip is one link after those before it, so a drawing holds a reference, not
a copy, and consecutive drawings under the same chain share one saved state when they are drawn.

Sections that set no draw order, and the counting passes that only count pages, draw directly as before.

## Consequences
- Content can be drawn over or under its neighbours without moving it.
- Pages that use draw order keep their drawing in memory until they end; pages that do not pay nothing.
- Where content lands is still known while it is held, for captured positions and links.
