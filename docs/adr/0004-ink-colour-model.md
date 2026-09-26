# 0004 — Colour is Ink: RGB, CMYK and spot, with tints

**Status:** Accepted

## Context
Colour was RGB only, with the Material Design palette built in. Print production needs more: CMYK process colour,
and named spot inks (PDF Separation colour spaces) for brand colours and special inks. A managed writer makes these
cheap to support, and few .NET PDF libraries offer them.

## Decision
Colour is modelled as `Ink`, in three kinds: RGB, CMYK and spot (a named ink with a process-colour fallback). Tints
behave as in print — a percentage of the ink. Only a handful of named inks ship (black, white, registration,
transparent); the Material palette is removed, and users bring their own colours.

## Consequences
- Print-ready output without post-processing, which is a genuine differentiator.
- Blending, gradients and transparency must be specified per colour space.
- Code using the Material palette must migrate; there are no external users yet.
