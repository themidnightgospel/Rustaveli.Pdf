# resvg's SVG test suite

The SVG documents of [resvg](https://github.com/linebender/resvg)'s test suite: small documents, each drawing one
element or property of SVG, many of them in odd, extreme or broken ways. Every one is read into artwork and drawn on a
page by `SvgCorpusTests` in the integration tests, as a PDF and as an image, and by `SvgCorpusTests` in the conformance
tests, whose PDFs must pass qpdf's check. Tests find them through `SvgCorpus` in `tests/Shared`.

They are unmodified copies of the `.svg` files under `crates/resvg/tests/tests/` at commit
[`75b6bbadd7999d0516dcd7153b4a321bfdf8670a`](https://github.com/linebender/resvg/tree/75b6bbadd7999d0516dcd7153b4a321bfdf8670a/crates/resvg/tests/tests),
in the same folders. The reference images, fonts and other resources beside them are not copied.

| Folder | Documents |
|---|---|
| `filters` | 396 |
| `masking` | 92 |
| `paint-servers` | 151 |
| `painting` | 305 |
| `shapes` | 133 |
| `structure` | 238 |
| `text` | 378 |

Documents that refer to a file outside themselves — an image, a style sheet or another document, on disk or on the
web — are left out, 29 of them; images carried in a `data:` URI are kept. Links of `<a>` elements are kept, since
nothing is fetched to follow them, and so are references such as `xlink:href="rect1"`, which name no file but test a
reference written without its `#`.

## Moving to a newer commit

1. Download the repository at the new commit and copy the `.svg` files under `crates/resvg/tests/tests/` here, in
   their folders, replacing these.
2. Remove those that refer to a file outside themselves: search them for `href` and `@import` values that do not
   start with `#` or `data:`.
3. Update the commit and the counts above.

## Licence

resvg is licensed under either of the Apache License 2.0 or the MIT licence, at the user's option. These files are
used under the MIT licence, reproduced here from resvg's `LICENSE-MIT` as its terms require:

```
Copyright 2017 the Resvg Authors

Permission is hereby granted, free of charge, to any
person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the
Software without restriction, including without
limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software
is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice
shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF
ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT
SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR
IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```
