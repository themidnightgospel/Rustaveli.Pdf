# Security policy

## Supported versions

Security fixes are released in the latest version of the packages. Older versions do not receive backported fixes.

| Version | Supported |
|---|---|
| Latest release | Yes |
| Older releases | No |

## Reporting a vulnerability

Do not disclose a suspected vulnerability, an exploit or a proof of concept in a public issue, discussion or pull
request.

Report it privately, through GitHub's
[private vulnerability reporting](https://github.com/themidnightgospel/Rustaveli.Pdf/security/advisories/new).
Include, where you can:

- what the vulnerability is and what it lets an attacker do;
- the package and version affected;
- the .NET runtime, operating system and architecture;
- the input that triggers it — a font, image, SVG or PDF file — or the smallest program that reproduces it;
- any mitigation you know of;
- whether you would like to be credited, and whether the report may be published once it is fixed.

## What happens next

- A report is acknowledged within seven days.
- A confirmed vulnerability is worked on in a private security advisory until a fix is released, and the advisory is
  then published.
- Fixes are prioritised by severity and how easily the vulnerability can be exploited.
- If a fix will take time, what can be done in the meantime is shared without disclosing what would put users at
  more risk.

Please allow reasonable time for a fix before making a vulnerability public.

## Scope

The library reads input that often comes from outside the program using it, and that is where most of its risk lies:

- **Fonts** — registered by the program or found installed on the machine, parsed by the core package.
- **Images** — JPEG and PNG files, decoded or embedded by the core package; recompressed by `Rustaveli.Pdf.Raster`.
- **SVG** — read into vector artwork by the core package.
- **Existing PDF files** — opened, repaired, decrypted and changed by `Rustaveli.Pdf.Operations`.
- **Text** — whatever a document is asked to set, including right-to-left text and complex scripts shaped by
  `Rustaveli.Pdf.Shaping`.

A crash other than the documented exception, a hang, memory or time out of proportion to the input, a read beyond
the input, or a document that exposes data it should not — an encrypted file readable without its password, say —
is in scope. So are the dependencies the packages ship with (SkiaSharp and HarfBuzzSharp in `Rustaveli.Pdf.Raster`
and `Rustaveli.Pdf.Shaping`, System.Memory on .NET Standard) and the build and release process.

A report found not to be a vulnerability may be answered with a suggestion to open an ordinary issue instead.

## Automated checks

Font parsing is fuzzed: fonts damaged at random must be used or rejected as malformed, with a `FormatException` —
never another exception, a hang or an allocation out of proportion to the file. Image decoding is bounded in pixels and in encoded
size, and the PDF reader in how deeply objects may nest. The repository also runs Dependabot updates for its NuGet
packages and GitHub Actions, CodeQL code scanning, and OpenSSF Scorecard. These reduce risk; they do not replace
review.

## Using the library safely

Treat fonts, images, SVG and PDF files from people you do not trust as untrusted input: process them where a crash
or a slow document cannot take other work down with it, and with limits on time and memory. Take the packages only
from nuget.org, keep them and the .NET runtime current, and review updates before adopting them.
