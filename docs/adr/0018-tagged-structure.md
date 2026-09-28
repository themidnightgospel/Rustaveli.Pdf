# 0018 — Structure is recorded as content is drawn

**Status:** Accepted

## Context
A tagged PDF records what its content is — headings, paragraphs, lists, tables, figures — in a tree of structure
elements, each pointing at the marked sequences of page content drawn for it; everything else is marked as
decoration. PDF/UA and the accessible levels of PDF/A require it. Content crosses pages, is drawn out of order under a
draw order, repeats in running heads and table header rows, and is laid out more than once while the page count
settles.

## Decision
The structure is built **while the final pass draws**, not from the composed tree. Blocks enter and leave elements on
a stack carried by the render context, and the surface is told, before each drawing, which element it belongs to —
or none, for decoration. A block creates its element the first time it draws and keeps it, so content going on to
later pages stays one element. Counting passes, and exports that are not tagged, create nothing.

What is tagged without asking is what cannot be anything else: text not inside other tagged content is a paragraph,
lists are lists, links are links. Tables are tagged only inside a `Table` tag, since tables are also used for
layout. The running head and foot, paper, underlay and overlay are decoration, as are repeats of a table's header
and footer rows after the first page. Images and drawings are decoration except inside a figure or formula.

The PDF surface marks content lazily: a drawing opens a marked sequence only if the one open belongs to a different
element, and a sequence is closed before any state blocks save or restore, so sequences nest properly within them.
Links become link elements holding their annotations. The tree is written once every page has been drawn, leaving
out elements that came to hold nothing.

## Consequences
- The reading order is the order content is drawn in, which is the composed order; draw order changes what lies
  on top, not what is read first.
- Tagging costs nothing when it is off, and one marked sequence per change of element when it is on.
- Surfaces that write no structure — images, SVG, XPS — ignore it.
- Heading levels in order and good descriptions of figures remain the author's to get right; the validator checks
  what can be checked, and the conformance tests run it over every specimen.
