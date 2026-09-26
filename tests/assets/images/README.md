# Test images

Small encoded images for the image loader: the JPEG variants the loader distinguishes. Tests read them from
`Path.Combine(AppContext.BaseDirectory, "assets", "images", name)`.

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
