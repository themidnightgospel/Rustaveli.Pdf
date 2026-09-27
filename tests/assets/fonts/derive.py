"""Builds the Specimen test fonts from the committed Noto Sans files.

    python derive.py

Requires fontTools (pip install fonttools). The output is committed, so this only needs running again to change
what the fonts contain. Each derived font covers Basic Latin plus a few characters that exercise one parser path:

SpecimenSans.ttc, a collection of three TrueType faces:
    0  Specimen Sans Regular   legacy 'kern' table (format 0) instead of GPOS; extra composite glyphs at
                               U+E000..U+E003 (nested, scaled, x/y-scaled, two-by-two)
    1  Specimen Sans SemiBold  weight 600 with typographic family/subfamily names (IDs 16/17); GPOS kerning
    2  Specimen Sans Italic    names on the Macintosh platform only (Mac Roman)
SpecimenCff-Regular.otf:     CFF outlines; GPOS kerning moved into extension lookups (type 9)

The names are changed so the derived faces never shadow the real Noto Sans family in matching tests.
"""

import os

from fontTools import subset
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables import otTables
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from fontTools.ttLib.ttCollection import TTCollection

HERE = os.path.dirname(os.path.abspath(__file__))
UNICODES = list(range(0x20, 0x7F)) + [0xA9, 0xC5, 0xE9]
COPYRIGHT = "Copyright 2022 The Noto Project Authors (https://github.com/notofonts/latin-greek-cyrillic)"
LICENSE = ("This Font Software is licensed under the SIL Open Font License, Version 1.1. "
           "This license is available with a FAQ at: https://scripts.sil.org/OFL")


def load_subset(file_name):
    font = TTFont(os.path.join(HERE, file_name))
    options = subset.Options()
    options.layout_features = ["kern"]
    options.name_IDs = ["*"]
    options.notdef_outline = True
    options.recalc_bounds = True
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)
    for tag in ("GSUB", "GDEF", "gasp", "DSIG"):
        if tag in font:
            del font[tag]
    return font


def set_names(font, family, subfamily, typographic=None, windows=True, mac=False):
    ps_family = family.replace(" ", "")
    full = family + " " + subfamily
    records = {
        0: COPYRIGHT,
        1: family,
        2: subfamily,
        3: "1.000;" + ps_family + "-" + subfamily,
        4: full,
        5: "Version 1.000",
        6: ps_family + "-" + subfamily,
        13: LICENSE,
        14: "https://scripts.sil.org/OFL",
    }
    if typographic:
        records[1] = typographic[0] + " " + typographic[1]
        records[2] = "Regular"
        records[4] = typographic[0] + " " + typographic[1]
        records[6] = typographic[0].replace(" ", "") + "-" + typographic[1]
        records[16] = typographic[0]
        records[17] = typographic[1]
    table = font["name"]
    table.names = []
    for name_id, text in sorted(records.items()):
        if windows:
            table.setName(text, name_id, 3, 1, 0x409)
        if mac:
            table.setName(text, name_id, 1, 0, 0)


def pair_kerning(font, left, right):
    """The kern feature's horizontal adjustment for a pair, summed across lookups as a shaper applies them."""
    table = font["GPOS"].table
    lookups = set()
    for record in table.FeatureList.FeatureRecord:
        if record.FeatureTag == "kern":
            lookups.update(record.Feature.LookupListIndex)
    total = 0
    for index in sorted(lookups):
        lookup = table.LookupList.Lookup[index]
        for subtable in lookup.SubTable:
            kind = lookup.LookupType
            if kind == 9:
                kind = subtable.ExtensionLookupType
                subtable = subtable.ExtSubTable
            if kind != 2 or left not in subtable.Coverage.glyphs:
                continue
            value = None
            if subtable.Format == 1:
                pairs = subtable.PairSet[subtable.Coverage.glyphs.index(left)].PairValueRecord
                match = [pair for pair in pairs if pair.SecondGlyph == right]
                if not match:
                    continue
                value = match[0].Value1
            else:
                first = subtable.ClassDef1.classDefs.get(left, 0)
                second = subtable.ClassDef2.classDefs.get(right, 0)
                value = subtable.Class1Record[first].Class2Record[second].Value1
            total += (getattr(value, "XAdvance", 0) or 0) if value is not None else 0
            break
    return total


def replace_gpos_with_kern_table(font):
    order = font.getGlyphOrder()
    pairs = {}
    for left in order:
        for right in order:
            value = pair_kerning(font, left, right)
            if value:
                pairs[(left, right)] = value
    subtable = KernTable_format_0()
    subtable.version = 0
    subtable.format = 0
    subtable.coverage = 1
    subtable.tupleIndex = None
    subtable.kernTable = pairs
    kern = newTable("kern")
    kern.version = 0
    kern.kernTables = [subtable]
    font["kern"] = kern
    del font["GPOS"]


def add_composites(font):
    """Composite glyphs Noto Sans lacks: nested, and each of the three scale encodings."""
    glyf = font["glyf"]
    hmtx = font["hmtx"]

    def component(name, x, y, transform=None):
        part = GlyphComponent()
        part.glyphName = name
        part.x = x
        part.y = y
        part.flags = 0x4
        if transform is not None:
            part.transform = transform
        return part

    specimens = [
        ("nestedring", [component("Aring", 0, 0), component("acute", 300, 400)]),
        ("scaledring", [component("A", 0, 0, [[0.5, 0], [0, 0.5]])]),
        ("stretchedring", [component("A", 0, 0, [[0.5, 0], [0, 0.75]])]),
        ("skewedring", [component("A", 0, 0, [[0.75, 0.25], [0.125, 0.875]])]),
    ]
    order = list(font.getGlyphOrder())
    for name, parts in specimens:
        glyph = Glyph()
        glyph.numberOfContours = -1
        glyph.components = parts
        order.append(name)
        glyf.glyphs[name] = glyph
        hmtx.metrics[name] = (639, 0)
    font.setGlyphOrder(order)
    glyf.glyphOrder = order
    for name, _ in specimens:
        glyf[name].recalcBounds(glyf)
        hmtx.metrics[name] = (639, glyf[name].xMin)
    for offset, (name, _) in enumerate(specimens):
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[0xE000 + offset] = name


def to_cff(font, family, subfamily):
    glyph_set = font.getGlyphSet()
    charstrings = {}
    for name in font.getGlyphOrder():
        pen = T2CharStringPen(width=font["hmtx"][name][0], glyphSet=glyph_set)
        glyph_set[name].draw(pen)
        charstrings[name] = pen.getCharString()
    for tag in ("glyf", "loca", "cvt ", "fpgm", "prep"):
        if tag in font:
            del font[tag]
    font.sfntVersion = "OTTO"
    ps_name = family.replace(" ", "") + "-" + subfamily
    builder = FontBuilder(font=font, isTTF=False)
    builder.setupCFF(ps_name, {"FullName": family + " " + subfamily, "FamilyName": family, "Weight": subfamily},
                     charstrings, {})
    maxp = font["maxp"]
    maxp.tableVersion = 0x00005000
    for field in ("maxPoints", "maxContours", "maxCompositePoints", "maxCompositeContours", "maxZones",
                  "maxTwilightPoints", "maxStorage", "maxFunctionDefs", "maxInstructionDefs", "maxStackElements",
                  "maxSizeOfInstructions", "maxComponentElements", "maxComponentDepth"):
        if hasattr(maxp, field):
            delattr(maxp, field)
    font["post"].formatType = 3.0
    font["post"].extraNames = []
    font["post"].mapping = {}


def wrap_pair_lookups_in_extensions(font):
    for lookup in font["GPOS"].table.LookupList.Lookup:
        if lookup.LookupType != 2:
            continue
        wrapped = []
        for subtable in lookup.SubTable:
            extension = otTables.ExtensionPos()
            extension.Format = 1
            extension.ExtensionLookupType = 2
            extension.ExtSubTable = subtable
            wrapped.append(extension)
        lookup.SubTable = wrapped
        lookup.SubTableCount = len(wrapped)
        lookup.LookupType = 9


def main():
    regular = load_subset("NotoSans-Regular.ttf")
    set_names(regular, "Specimen Sans", "Regular")
    replace_gpos_with_kern_table(regular)
    add_composites(regular)

    semibold = load_subset("NotoSans-Bold.ttf")
    set_names(semibold, "Specimen Sans", "Regular", typographic=("Specimen Sans", "SemiBold"))
    semibold["OS/2"].usWeightClass = 600
    semibold["OS/2"].fsSelection = 0x40 | 0x80
    semibold["head"].macStyle = 0

    italic = load_subset("NotoSans-Italic.ttf")
    set_names(italic, "Specimen Sans", "Italic", windows=False, mac=True)

    collection = TTCollection()
    collection.fonts = [regular, semibold, italic]
    collection.save(os.path.join(HERE, "SpecimenSans.ttc"))

    cff = load_subset("NotoSans-Regular.ttf")
    set_names(cff, "Specimen Cff", "Regular")
    to_cff(cff, "Specimen Cff", "Regular")
    wrap_pair_lookups_in_extensions(cff)
    cff.save(os.path.join(HERE, "SpecimenCff-Regular.otf"))


if __name__ == "__main__":
    main()
