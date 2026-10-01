# Font format corpus

Fonts in the formats the Noto files one folder up never exercise, for the tests that register, measure, set, embed
and draw text in each: `FontFormatCorpusTests` (integration) and `CorpusFontEmbeddingTests` (conformance).

| File | Format | Source | How it was made | Licence |
|---|---|---|---|---|
| `SourceSans3VF-Upright.ttf` | Variable TrueType: `glyf` outlines varied by `gvar`, one `wght` axis from 200 to 900, default 200 | Source Sans 3 by Adobe, release [3.052R](https://github.com/adobe-fonts/source-sans/releases/tag/3.052R), asset `VF-source-sans-3.052R.zip`, file `VF/SourceSans3VF-Upright.ttf` | Unmodified, under its original name | SIL Open Font License 1.1, Reserved Font Name "Source": [SourceSans3-LICENSE.md](SourceSans3-LICENSE.md) |
| `SourceSans3VF-Upright.otf` | Variable OpenType with `CFF2` outlines, the same axis | As above, file `VF/SourceSans3VF-Upright.otf` | Unmodified, under its original name | As above |
| `NotoSans-Collection.ttc` | TrueType collection of two faces: #0 Noto Sans Regular, #1 Noto Sans Bold | [`NotoSans-Regular.ttf`](../NotoSans-Regular.ttf) and [`NotoSans-Bold.ttf`](../NotoSans-Bold.ttf) in this repository | Combined by `build.py` below, sharing identical tables; the faces are otherwise unchanged | SIL Open Font License 1.1: [OFL.txt](../OFL.txt) |
| `KernTableTest-Regular.ttf` | TrueType kerned only by a `kern` table (format 0) | [`NotoSans-Regular.ttf`](../NotoSans-Regular.ttf) in this repository | Modified by `build.py` below: the `kern` feature and its lookups removed from `GPOS` (its `mark` and `mkmk` stay), a `kern` table added with the pairs AV, VA, AT, To, Ta, Yo, LT, P. and Wa, and renamed "Kern Table Test" so it is never taken for Noto Sans | SIL Open Font License 1.1: [OFL.txt](../OFL.txt) |

The Source Sans 3 files are committed exactly as Adobe released them, since "Source" is a Reserved Font Name and a
modified version could not keep it. Their SHA-256 digests are:

```
1147db9a3f0edd4956068de77930148acce2742dd76d57f7239b2b1c687ac63f  SourceSans3VF-Upright.ttf
3d0dfd6a3a644ab3d462a737923ffac41fb0ae007ce9ba83c24e6bfa76aa56c7  SourceSans3VF-Upright.otf
```

To fetch them again:

```
gh release download 3.052R -R adobe-fonts/source-sans -p "VF-source-sans-3.052R.zip"
```

Noto Sans has no Reserved Font Name, so the OFL permits both the collection and the modified copy, which stay under
it. Their copyright and licence names are kept from Noto Sans.

## build.py

Builds the two Noto-derived files with [fontTools](https://github.com/fonttools/fonttools) 4.62
(`pip install fonttools`). It keeps the sources' modification times, so a rebuild gives the same bytes. Run it from
a scratch folder:

```
python build.py <tests/assets/fonts> <output folder>
```

```python
"""Builds NotoSans-Collection.ttc and KernTableTest-Regular.ttf from the Noto Sans files in tests/assets/fonts.

    python build.py <tests/assets/fonts> <output folder>
"""

import os
import sys

import fontTools.subset  # noqa: F401  (adds prune_lookups to the GPOS table)
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from fontTools.ttLib.ttCollection import TTCollection

FONTS, OUT = sys.argv[1], sys.argv[2]

# Kerning in font units (1000 per em), between the glyphs of the characters named.
KERN_PAIRS = {
    ("A", "V"): -80, ("V", "A"): -80, ("A", "T"): -60, ("T", "o"): -90, ("T", "a"): -90,
    ("Y", "o"): -100, ("L", "T"): -110, ("P", "."): -150, ("W", "a"): -50,
}


def load(name):
    # Keeping the source's modification time makes a rebuild byte for byte the same.
    return TTFont(os.path.join(FONTS, name), recalcTimestamp=False)


def build_collection():
    collection = TTCollection()
    collection.fonts = [load(name) for name in ("NotoSans-Regular.ttf", "NotoSans-Bold.ttf")]
    collection.save(os.path.join(OUT, "NotoSans-Collection.ttc"), shareTables=True)


def drop_gpos_kern_feature(font):
    gpos = font["GPOS"].table
    records = gpos.FeatureList.FeatureRecord
    kept = [index for index, record in enumerate(records) if record.FeatureTag != "kern"]
    renumbered = {old: new for new, old in enumerate(kept)}
    gpos.FeatureList.FeatureRecord = [records[index] for index in kept]
    gpos.FeatureList.FeatureCount = len(kept)
    for script in gpos.ScriptList.ScriptRecord:
        systems = [script.Script.DefaultLangSys] + [record.LangSys for record in script.Script.LangSysRecord]
        for system in systems:
            if system is None:
                continue
            system.FeatureIndex = [renumbered[index] for index in system.FeatureIndex if index in renumbered]
            system.FeatureCount = len(system.FeatureIndex)
            if system.ReqFeatureIndex != 0xFFFF:
                system.ReqFeatureIndex = renumbered.get(system.ReqFeatureIndex, 0xFFFF)
    assert getattr(gpos, "FeatureVariations", None) is None
    font["GPOS"].prune_lookups()


def rename(font, family, postscript):
    names = font["name"]
    for name_id in (16, 17, 21, 22, 25):
        names.removeNames(nameID=name_id)
    for name_id, value in ((1, family), (2, "Regular"), (3, family + " Regular"), (4, family + " Regular"), (6, postscript)):
        names.removeNames(nameID=name_id)
        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)


def build_kern_table_font():
    font = load("NotoSans-Regular.ttf")
    drop_gpos_kern_feature(font)

    glyph_of = font.getBestCmap()
    table = KernTable_format_0()
    table.version = 0
    table.format = 0
    table.coverage = 1
    table.tupleIndex = None
    table.kernTable = {(glyph_of[ord(left)], glyph_of[ord(right)]): value for (left, right), value in KERN_PAIRS.items()}
    font["kern"] = newTable("kern")
    font["kern"].version = 0
    font["kern"].kernTables = [table]

    rename(font, "Kern Table Test", "KernTableTest-Regular")
    font.save(os.path.join(OUT, "KernTableTest-Regular.ttf"))


build_collection()
build_kern_table_font()
```

The output's SHA-256 digests, from the Noto Sans files committed beside it:

```
b699093ace4bdcbe397892b20554bbef0ea35e8876def76b20e6ca3f109e9a48  NotoSans-Collection.ttc
59246addfc29a80cb11f0af62f880236d1b8cfdc6cc5f8b9520f51a6e02d4554  KernTableTest-Regular.ttf
```
