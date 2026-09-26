# Unicode Character Database files

Unmodified files from the Unicode Character Database, version 16.0.0, that the line breaker and the bidirectional
algorithm are built from and tested against. They are committed exactly as published.

| File | Used for | Source |
|---|---|---|
| `LineBreak.txt` | Line_Break of every code point | <https://www.unicode.org/Public/16.0.0/ucd/LineBreak.txt> |
| `EastAsianWidth.txt` | the `$EastAsian` set of rules LB19a, LB21a and LB30 | <https://www.unicode.org/Public/16.0.0/ucd/EastAsianWidth.txt> |
| `DerivedGeneralCategory.txt` | resolving SA (LB1), splitting QU into initial and final punctuation (LB15a, LB15b, LB19), unassigned code points (LB30b) | <https://www.unicode.org/Public/16.0.0/ucd/extracted/DerivedGeneralCategory.txt> |
| `emoji-data.txt` | Extended_Pictographic (LB30b) | <https://www.unicode.org/Public/16.0.0/ucd/emoji/emoji-data.txt> |
| `LineBreakTest.txt` | the line breaking conformance test, `LineBreakConformanceTests` | <https://www.unicode.org/Public/16.0.0/ucd/auxiliary/LineBreakTest.txt> |
| `DerivedBidiClass.txt` | Bidi_Class of every code point, defaults for unassigned ones included (`@missing` lines) | <https://www.unicode.org/Public/16.0.0/ucd/extracted/DerivedBidiClass.txt> |
| `BidiBrackets.txt` | Bidi_Paired_Bracket and Bidi_Paired_Bracket_Type, which rule N0 pairs brackets by | <https://www.unicode.org/Public/16.0.0/ucd/BidiBrackets.txt> |
| `BidiMirroring.txt` | Bidi_Mirroring_Glyph, the mirrored character rule L4 draws | <https://www.unicode.org/Public/16.0.0/ucd/BidiMirroring.txt> |
| `BidiTest.txt` | bidi conformance: every sequence of classes up to length 4, and longer ones, per paragraph direction | <https://www.unicode.org/Public/16.0.0/ucd/BidiTest.txt> |
| `BidiCharacterTest.txt` | bidi conformance: sequences of characters, bracket pairs among them | <https://www.unicode.org/Public/16.0.0/ucd/BidiCharacterTest.txt> |

The algorithms are [UAX #14, revision 53](https://www.unicode.org/reports/tr14/tr14-53.html) and
[UAX #9, revision 50](https://www.unicode.org/reports/tr9/tr9-50.html), the versions that go with Unicode 16.0.0.

## Generated tables

- `dotnet run eng/unicode-line-break.cs` reads the first four files and writes
  `src/Rustaveli.Pdf/Text/LineBreaking/LineBreakTable.cs`.
- `dotnet run eng/unicode-bidi.cs` reads `DerivedBidiClass.txt`, `BidiBrackets.txt` and `BidiMirroring.txt` and writes
  `src/Rustaveli.Pdf/Text/Bidi/BidiCharacterTables.cs`.

The generated tables are committed, so building the library needs neither these files nor the scripts. The unit tests
read the files again, with parsers of their own, and compare the tables with them for every code point; the
conformance tests run every case of the test files.

## Moving to a newer version

1. Download the same files for the new version from https://www.unicode.org/Public/, replacing these.
2. Update the version in both scripts and run them.
3. Read the new revisions of UAX #14 and UAX #9 against `LineBreakEnumerator` and `BidiParagraph`, rule by rule: the
   rules change between versions about as often as the data does, and the conformance tests fail until they match.

## Licence

These files are distributed under the Unicode License v3, from <https://www.unicode.org/license.txt>, and so are the
tables generated from them, which ship in the Rustaveli.Pdf package. The licence is reproduced here as its terms
require:

```
UNICODE LICENSE V3

COPYRIGHT AND PERMISSION NOTICE

Copyright © 1991-2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.
```

The files' own terms of use are at https://www.unicode.org/terms_of_use.html.
