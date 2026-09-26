# Unicode Character Database files

Unicode 16.0.0 data that the line breaker is built from and tested against. They are committed unchanged, exactly as
published.

| File | Source | Used for |
| --- | --- | --- |
| `LineBreak.txt` | https://www.unicode.org/Public/16.0.0/ucd/LineBreak.txt | Line_Break of every code point |
| `EastAsianWidth.txt` | https://www.unicode.org/Public/16.0.0/ucd/EastAsianWidth.txt | the `$EastAsian` set of rules LB19a, LB21a and LB30 |
| `DerivedGeneralCategory.txt` | https://www.unicode.org/Public/16.0.0/ucd/extracted/DerivedGeneralCategory.txt | resolving SA (LB1), splitting QU into initial and final punctuation (LB15a, LB15b, LB19), unassigned code points (LB30b) |
| `emoji-data.txt` | https://www.unicode.org/Public/16.0.0/ucd/emoji/emoji-data.txt | Extended_Pictographic (LB30b) |
| `LineBreakTest.txt` | https://www.unicode.org/Public/16.0.0/ucd/auxiliary/LineBreakTest.txt | the conformance test, `LineBreakConformanceTests` |

The rules themselves are [UAX #14, revision 53](https://www.unicode.org/reports/tr14/tr14-53.html), the version of the
Unicode Line Breaking Algorithm that goes with Unicode 16.0.0.

`eng/unicode-line-break.cs` reads the first four files and writes
`src/Rustaveli.Pdf/Text/LineBreaking/LineBreakTable.cs`. `LineBreakPropertiesTests` reads them again, independently,
and checks the generated table against them for every code point.

## Moving to a newer version

1. Download the same five files for the new version from https://www.unicode.org/Public/, replacing these.
2. Update the version in `eng/unicode-line-break.cs` and run `dotnet run eng/unicode-line-break.cs`.
3. Read the new revision of UAX #14 against `LineBreakEnumerator`, rule by rule: the rules change between versions
   about as often as the data does, and the conformance test will fail until they match.

## Licence

These files are distributed under the Unicode License v3, and so is the table generated from them, which ships in the
Rustaveli.Pdf package:

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
