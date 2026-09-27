# Test images

Small encoded images for the image loader: every PNG colour type and bit depth, interlaced and not, every kind of
transparency, and the JPEG variants the loader distinguishes. Tests read them from
`Path.Combine(AppContext.BaseDirectory, "assets", "images", name)`.

## PngSuite

Files named in PngSuite's scheme (`basn0g01.png`, `tbbn3p08.png`, …) are unmodified copies from
[PngSuite](http://www.schaik.com/pngsuite/) (release 2017-07-19) by Willem van Schaik, distributed under this
licence:

> Permission to use, copy, modify and distribute these images for any purpose and without fee is hereby granted.
>
> (c) Willem van Schaik, 1996, 2011

The name encodes the content: the first letters give the feature under test, `n`/`i` non-interlaced or interlaced,
then the colour type (`0g` gray, `2c` RGB, `3p` palette, `4a` gray + alpha, `6a` RGBA) and the bit depth.

| Files | What they cover |
|---|---|
| `basn*`, `basi*` | Every legal colour type and bit depth, each also Adam7-interlaced with identical pixels |
| `s01*`–`s09*` | 1 × 1 to 9 × 9 images, interlaced and not: Adam7 passes that are empty or one pixel wide |
| `f00n0g08`–`f04n0g08`, `f99n0g04` | Each row filter type, and all of them mixed at 4 bits |
| `tbbn0g04`, `tbwn0g16`, `tbrn2c08`, `tbbn2c16` | tRNS colour keys for gray and RGB, 4 to 16 bits |
| `tbbn3p08`, `tp1n3p08`, `tm3n3p02` | Palette alpha: one fully transparent entry; three levels of transparency |
| `oi4n2c16`, `oi9n0g16` | Image data split over 4 IDAT chunks, and over one-byte IDAT chunks |
| `z00n2c08`, `z09n2c08` | zlib stored (uncompressed) blocks, and maximum compression |
| `exif2c08` | An eXIf chunk |
| `g03n0g16`, `ccwn2c08` | gAMA and cHRM chunks |
| `x*` | Corrupt files: bad signatures, CRCs, colour types and bit depths, missing IDAT |

## Generated

Written with Pillow 12.3 from one 48 × 32 test pattern — wider than tall, so a swapped width and height cannot
pass — in a throwaway script that is not kept. They are released under the repository's licence.

| File | How it was written |
|---|---|
| `jpeg-baseline.jpg` | Baseline (SOF0) YCbCr with a JFIF segment, quality 90 |
| `jpeg-progressive.jpg` | Progressive (SOF2), otherwise as above |
| `jpeg-gray.jpg` | One component |
| `jpeg-cmyk-adobe.jpg` | Four components with an Adobe APP14 segment, CMYK stored inverted as Photoshop does |
| `jpeg-rgb-adobe.jpg` | Three components coded as RGB (`keep_rgb`), component IDs `R G B`, Adobe transform 0 |
| `jpeg-exif-orientation6.jpg` | EXIF orientation 6 (rotate 90° clockwise to view), little-endian |
| `jpeg-icc.jpg` | A 588-byte sRGB ICC profile in one APP2 segment |
| `png-iccp.png` | 8-bit RGB with the same profile in an iCCP chunk |
