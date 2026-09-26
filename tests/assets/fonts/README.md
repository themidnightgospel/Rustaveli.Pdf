# Test fonts

Fonts committed so that every test measures and renders with identical metrics on Windows, Linux and macOS. A test
that names a system font ("Arial") silently gets a substitute on a machine that lacks it — a different width per
glyph, different line breaks, different page breaks — and passes or fails depending on where it runs.

| File | Covers | Source |
|---|---|---|
| `NotoSans-Regular.ttf`, `-Bold.ttf`, `-Italic.ttf` | Latin, Greek, Cyrillic | [notofonts/latin-greek-cyrillic](https://github.com/notofonts/latin-greek-cyrillic) |
| `NotoSansGeorgian-Regular.ttf` | Georgian — a script Noto Sans lacks, for fallback tests | [notofonts/georgian](https://github.com/notofonts/georgian) |

All are licensed under the SIL Open Font License 1.1 ([OFL.txt](OFL.txt)), which permits redistribution with
software.
