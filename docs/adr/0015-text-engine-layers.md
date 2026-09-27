# 0015 — The text engine in layers: Unicode rules in the core, complex shaping by opt-in

**Status:** Accepted. Refines [ADR 0006](0006-packages-and-dependencies.md), which placed bidi in the native package.

## Context
Setting text well takes several separate pieces of work, and they differ in whether they need native code:

- **Where lines may break** is the Unicode line breaking algorithm (UAX #14): tables and rules, no fonts involved.
- **The order runs appear in** is the Unicode bidirectional algorithm (UAX #9): tables and rules again.
- **Which glyphs set a run** is font work. For Latin, Greek, Cyrillic, Georgian and CJK it is pair kerning, standard
  ligatures and a handful of other `GSUB` features, which a managed reader of the font's tables does exactly. For
  Arabic joining, Indic reordering and other complex scripts it is a large, script-specific body of logic that
  HarfBuzz has refined for years.

Putting the whole engine behind a native dependency would give up the dependency-free core ([ADR 0006](0006-packages-and-dependencies.md))
for the sake of scripts most documents never use; putting none of it there would mean re-implementing HarfBuzz.

## Decision
The core carries the Unicode algorithms and the font features that need no script-specific logic:

- UAX #14 line breaking and UAX #9 bidirectional ordering, from Unicode's own data files, tested against Unicode's
  conformance files;
- `GSUB` substitution (every lookup type) and `GPOS` pair kerning, with the default feature set per script and a
  way for a type style to switch features on and off;
- the glyph walk of [ADR 0014](0014-text-is-shaped-once.md), extended so that one run yields glyphs with the
  clusters they came from.

Complex-script shaping stays in the opt-in `Rustaveli.Pdf.Shaping` package, which plugs a HarfBuzz shaper into the
same seam for runs whose script needs it. Bidi moves out of that package: reordering is not shaping, and Hebrew,
for one, reads correctly with bidi alone.

## Consequences
- The default install sets mixed left-to-right and right-to-left text in the right order, breaks lines where Unicode
  allows, and applies ligatures and kerning, with no native code.
- Arabic, Indic and similar scripts are correct only with the Shaping package; without it their glyphs are set
  unjoined and unreordered, and the core says so rather than failing.
- Each algorithm is checked against its published conformance data, so it can be trusted without reading the code.
