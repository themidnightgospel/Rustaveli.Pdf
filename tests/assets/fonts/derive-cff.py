"""Builds the subroutinized CFF test fonts from two published OpenType fonts.

    python derive-cff.py <folder holding the two source fonts>

Requires fontTools (pip install fonttools). The sources are too large to commit, and the output is committed, so this
only needs running again to change what the fonts contain. Download the sources first:

    https://github.com/adobe-fonts/source-sans/raw/release/OTF/SourceSans3-Regular.otf
    https://github.com/notofonts/noto-cjk/raw/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf

SpecimenSubrs-Regular.otf:  from Source Sans 3 — a name-keyed CFF whose glyphs call local and global subroutines,
                            as most Latin OpenType fonts' do
SpecimenCjk-Regular.otf:    from Noto Sans CJK SC — a CID-keyed CFF with several font dicts, each with its own
                            subroutines, and glyphs chosen from several of them, as Chinese, Japanese and Korean
                            OpenType fonts are made

Subsetting keeps the subroutines the kept glyphs call, as fontTools does unless told to desubroutinize. Both fonts
are renamed: the sources' names are reserved by their licence, and the specimens must never shadow a real family.
"""

import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
LICENSE = ("This Font Software is licensed under the SIL Open Font License, Version 1.1. "
           "This license is available with a FAQ at: https://scripts.sil.org/OFL")

LATIN = list(range(0x20, 0x7F)) + list(range(0xA0, 0x100))

# Common Chinese characters, CJK punctuation, full-width forms and a little kana: enough glyphs, drawn from several
# of the source's font dicts, to exercise its subroutines the way real text does.
HANZI = ("的一是不了人我在有他这中大来上国个到说们为子和你地出道也时年得就那要下以生会自着去之过家学对可里后小么"
         "心多天而能好都然没日于起还发成事只作当想看文无开手十用主行方又如前所本见经头面公同三已老从动两长知民样现"
         "分将外但身些与高意进把法此实回二理美点月明其种声全工己话儿者向情部正名定女问力机给等几很业最间新什打便位"
         "因重被走电四第门相次东政海口使教西再平真听世气信北少关并内加化由却代军产入先山五太水万市眼体别处总才场师"
         "书永鬱龘")
PUNCTUATION = "、。「」『』（），：；！？《》"
KANA = "あいうえおかきくけこアイウエオカキクケコ"


def subset_keeping_subroutines(path, unicodes):
    font = TTFont(path)
    options = subset.Options()
    options.layout_features = []
    options.name_IDs = ["*"]
    options.notdef_outline = True
    options.desubroutinize = False
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=unicodes)
    subsetter.subset(font)
    for tag in ("GSUB", "GPOS", "GDEF", "BASE", "VORG", "vhea", "vmtx", "DSIG"):
        if tag in font:
            del font[tag]
    return font


def rename(font, family):
    """Names the font family Regular everywhere a name is kept: the name table, the CFF and its font dicts."""
    ps_name = family.replace(" ", "") + "-Regular"
    copyright_notice = font["name"].getDebugName(0)
    records = {
        0: copyright_notice,
        1: family,
        2: "Regular",
        3: "1.000;" + ps_name,
        4: family + " Regular",
        5: "Version 1.000",
        6: ps_name,
        13: LICENSE,
        14: "https://scripts.sil.org/OFL",
    }
    table = font["name"]
    table.names = []
    for name_id, text in sorted(records.items()):
        table.setName(text, name_id, 3, 1, 0x409)

    # CFF strings are Latin-1: the notice's curly quotes become straight ones there.
    cff_notice = copyright_notice.replace("‘", "'").replace("’", "'")
    cff = font["CFF "].cff
    top = cff[cff.fontNames[0]]
    cff.fontNames = [ps_name]
    cff.topDictIndex[0] = top
    for key in ("FullName", "FamilyName", "Notice", "Copyright"):
        if hasattr(top, key):
            setattr(top, key, family + " Regular" if key == "FullName" else family if key == "FamilyName" else cff_notice)
    for index, fd in enumerate(getattr(top, "FDArray", [])):
        fd.FontName = ps_name + "-" + str(index)


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)

    sources = sys.argv[1]

    subrs = subset_keeping_subroutines(os.path.join(sources, "SourceSans3-Regular.otf"), LATIN)
    rename(subrs, "Specimen Subrs")
    subrs.save(os.path.join(HERE, "SpecimenSubrs-Regular.otf"))

    cjk_text = HANZI + PUNCTUATION + KANA
    cjk = subset_keeping_subroutines(
        os.path.join(sources, "NotoSansCJKsc-Regular.otf"), LATIN + sorted({ord(character) for character in cjk_text}))
    rename(cjk, "Specimen Cjk")
    cjk.save(os.path.join(HERE, "SpecimenCjk-Regular.otf"))


if __name__ == "__main__":
    main()
