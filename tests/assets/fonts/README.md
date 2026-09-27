# Test fonts

Fonts committed so that every test measures and renders with identical metrics on Windows, Linux and macOS. A test
that names a system font ("Arial") silently gets a substitute on a machine that lacks it — a different width per
glyph, different line breaks, different page breaks — and passes or fails depending on where it runs.

| File | Covers | Source |
|---|---|---|
| `NotoSans-Regular.ttf`, `-Bold.ttf`, `-Italic.ttf` | Latin, Greek, Cyrillic | [notofonts/latin-greek-cyrillic](https://github.com/notofonts/latin-greek-cyrillic) |
| `NotoSansGeorgian-Regular.ttf` | Georgian — a script Noto Sans lacks, for fallback tests | [notofonts/georgian](https://github.com/notofonts/georgian) |
| `NotoSansArabic-Regular.ttf` | Arabic — joining, ligatures and marks, for complex-script shaping | [notofonts/arabic](https://github.com/notofonts/arabic) |
| `NotoSansDevanagari-Regular.ttf` | Devanagari — reordering and conjuncts, for complex-script shaping | [notofonts/devanagari](https://github.com/notofonts/devanagari) |
| `SpecimenSans.ttc` | A font collection of three TrueType faces, for the font parser | Derived from Noto Sans by `derive.py` |
| `SpecimenCff-Regular.otf` | CFF (PostScript) outlines, for the font parser | Derived from Noto Sans by `derive.py` |
| `SpecimenLayout-Regular.otf` | Every kind of glyph substitution, for the GSUB engine | Derived from Noto Sans by `derive.py` |

All are licensed under the SIL Open Font License 1.1 ([OFL.txt](OFL.txt)), which permits redistribution with
software, and modified versions under the same licence.

## The Specimen fonts

`derive.py` builds them from the Noto Sans files above with [fontTools](https://github.com/fonttools/fonttools)
(`pip install fonttools`, then `python derive.py`). Each keeps Basic Latin plus `©`, `Å` and `é`, is renamed so it
never shadows the real Noto Sans family, and exercises structures the Noto files do not contain:

| Face | What it adds |
|---|---|
| `SpecimenSans.ttc` #0, Specimen Sans Regular | A legacy `kern` table (format 0) in place of GPOS; composite glyphs at U+E000–U+E003: one nested (Å, itself a composite, plus an acute), and one for each of the three component scale encodings |
| `SpecimenSans.ttc` #1, Specimen Sans SemiBold | Weight 600 with typographic family and subfamily names (IDs 16 and 17), the legacy family being "Specimen Sans SemiBold" |
| `SpecimenSans.ttc` #2, Specimen Sans Italic | Names on the Macintosh platform only, in Mac Roman |
| `SpecimenCff-Regular.otf` | CFF outlines converted from the TrueType ones; GPOS kerning moved into extension lookups (type 9) |
| `SpecimenLayout-Regular.otf` | CFF outlines, with a GSUB and GDEF compiled by fontTools from the feature file in `derive.py`: features `ss01` to `ss10` and `salt` each exercise one kind of substitution — ligatures past marks, glyph and class contexts, nested lookups that lengthen or shorten the input, reverse chaining, mark filtering sets, mark attachment types, extension lookups and alternates. In place of `©`, `Å` and `é` it keeps U+0131, U+0300, U+0301, U+0323 and U+FB00 to U+FB03 |

The copyright and licence names (IDs 0, 13 and 14) are kept from Noto Sans. To rebuild only the layout specimen, run
`python -c "import derive; derive.build_layout_specimen()"`.
