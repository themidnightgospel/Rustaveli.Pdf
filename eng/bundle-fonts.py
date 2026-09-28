"""Builds the typefaces bundled into the core package from the committed Noto Sans files.

    python eng/bundle-fonts.py

Requires fontTools (pip install fonttools). The output is committed under src/Rustaveli.Pdf/Fonts/Bundled, so this
only needs running again to change what the bundled faces cover.

The bundled faces are the last resort when a document names a typeface that is neither registered nor installed and
no common substitute exists either — a container image with no fonts at all, say — so that text still sets. They
cover Latin, Greek and Cyrillic with the punctuation, currency and symbols documents commonly use; kerning is kept,
hinting is dropped, since PDF viewers do not use it and it is most of each file.

Noto Sans is licensed under the SIL Open Font License 1.1, which permits bundling; the licence travels with the files.
"""

import os
import shutil

from fontTools import subset

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "..", "tests", "assets", "fonts")
TARGET = os.path.join(HERE, "..", "src", "Rustaveli.Pdf", "Fonts", "Bundled")

UNICODES = (
    list(range(0x0020, 0x007F))      # Basic Latin
    + list(range(0x00A0, 0x0180))    # Latin-1 Supplement and Latin Extended-A
    + [0x0192, 0x0218, 0x0219, 0x021A, 0x021B, 0x02C6, 0x02C7, 0x02D8, 0x02D9, 0x02DA, 0x02DB, 0x02DC, 0x02DD]
    + list(range(0x0384, 0x03CF))    # Greek
    + list(range(0x0400, 0x0460))    # Cyrillic
    + list(range(0x2010, 0x2028))    # Dashes, quotes, bullets, ellipsis
    + list(range(0x2030, 0x203B))    # Per mille, primes, angle quotes
    + [0x2044, 0x2070, 0x2074, 0x2075, 0x2076, 0x2077, 0x2078, 0x2079, 0x207F]
    + list(range(0x2080, 0x208A))    # Subscript digits
    + [0x20A3, 0x20A4, 0x20A6, 0x20A7, 0x20A9, 0x20AA, 0x20AB, 0x20AC, 0x20B9, 0x20BA, 0x20BD, 0x20BE]
    + [0x2113, 0x2116, 0x2122, 0x2126, 0x212E, 0x2190, 0x2191, 0x2192, 0x2193]
    + [0x2202, 0x2206, 0x220F, 0x2211, 0x2212, 0x2215, 0x2219, 0x221A, 0x221E, 0x222B]
    + [0x2248, 0x2260, 0x2264, 0x2265, 0x25CA, 0xFB01, 0xFB02, 0xFFFD]
)

FACES = ["NotoSans-Regular.ttf", "NotoSans-Bold.ttf", "NotoSans-Italic.ttf", "NotoSans-BoldItalic.ttf"]


def main():
    os.makedirs(TARGET, exist_ok=True)

    options = subset.Options()
    options.layout_features = ["kern", "liga"]
    options.hinting = False
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.notdef_outline = True
    options.glyph_names = False

    for face in FACES:
        font = subset.load_font(os.path.join(SOURCE, face), options)
        subsetter = subset.Subsetter(options)
        subsetter.populate(unicodes=UNICODES)
        subsetter.subset(font)
        path = os.path.join(TARGET, face)
        subset.save_font(font, path, options)
        print(f"{face}: {os.path.getsize(path):,} bytes")

    shutil.copyfile(os.path.join(SOURCE, "OFL.txt"), os.path.join(TARGET, "OFL.txt"))


if __name__ == "__main__":
    main()
