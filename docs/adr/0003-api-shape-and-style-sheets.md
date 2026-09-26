# 0003 — Keep the fluent shape; add named style sheets

**Status:** Accepted

## Context
The fluent construction style — decorators chained onto a slot, ending in content, with lambdas describing compound
blocks — is ergonomic and proven. Replacing it with a declarative object tree would be a large redesign with
uncertain benefit to users.

What documents do lack is a way to say *"this is a heading"* once and apply it everywhere. Print tools solved this
long ago with paragraph, character and object styles.

## Decision
Keep the fluent shape. Add style sheets: named paragraph, character and frame styles, defined once, able to inherit
from one another (`BasedOn`), and applied by name.

## Consequences
- Consistent documents need far less repetition, and a house style can be shared as a single object.
- Style resolution — inheritance, and precedence between named styles and inline overrides — must be specified and
  tested as carefully as layout.
