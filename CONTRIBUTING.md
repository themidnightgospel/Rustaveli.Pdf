# Contributing

Thank you for helping. This guide covers getting the repository building, what a change needs before it can merge,
and where the longer explanations live.

## Getting started

You need the .NET SDK named in [`global.json`](global.json). Windows runs every test suite, including the .NET
Framework 4.8 legs; Linux and macOS run all but those.

```bash
git clone https://github.com/themidnightgospel/Rustaveli.Pdf.git
cd Rustaveli.Pdf
dotnet run eng/tools.cs       # once: fetches the validators the conformance tests use
dotnet build -c Release
dotnet test
```

`eng/tools.cs` fetches veraPDF everywhere, and qpdf and a Java runtime on Windows; on Linux and macOS install qpdf
and a Java runtime with your package manager. The font-fallback tests also need CJK and Georgian fonts, which Windows
and macOS have and Debian or Ubuntu get from `fonts-noto-core` and `fonts-noto-cjk`.

## Making a change

- **Work on a branch** and open a pull request against `main`. Small commits that each build and pass make a change
  easy to review and to bisect.
- **Write it the way the code around it is written.** The standing decisions — explicit types, one type per file,
  comments that explain why, one public namespace — are in [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md). Most are
  enforced by the build: in Release, a warning is an error.
- **Test what you change.** Unit tests run the layout engine against a fake type measurer, so expected values can
  be worked out by hand; integration tests read real PDFs back; conformance tests check files with qpdf and veraPDF
  and compare pages rendered by PDFium with approved snapshots. [How it's tested](docs/testing.md)
  explains each.
- **Keep coverage up.** `dotnet run eng/coverage.cs` runs the suites with coverage and fails below the floors in
  [`eng/coverage-thresholds.json`](eng/coverage-thresholds.json). A test should assert what a caller would see, not
  just run the code.
- **Approve visual changes deliberately.** When a change alters what pages look like, the conformance tests write
  the new renderings to `artifacts/snapshots`. Look at the received and diff images, then approve them with
  `dotnet run eng/approve-snapshots.cs`, naming the specimens you mean to approve.
- **Say what changed** in [`CHANGELOG.md`](CHANGELOG.md) when users will notice it, and in the
  [parity checklist](docs/parity/PARITY.md) when it adds or completes a capability.

## Documentation

The [documentation site](https://themidnightgospel.github.io/Rustaveli.Pdf/) is built from `docs/` by MkDocs
Material, and its API reference from the packages' XML documentation by DocFX. To preview it while writing:

```bash
python -m pip install --require-hashes -r docs/requirements.txt
mkdocs serve                              # the pages, redrawn as you save
dotnet docfx docs/api/docfx.json --serve  # the API reference
```

A page must be listed in `mkdocs.yml` to appear in the navigation, and every link must resolve: the build is strict,
and a pull request that breaks it fails. Every C# example in the guides is compiled and run by the tests.

## Pull requests

Every push to a pull request runs the build and the tests on Linux (with the coverage gate) and on Windows (on
.NET 10 and .NET Framework 4.8). Once a pull request is ready, the `ready-to-merge` label runs the costlier checks
too: macOS, and the benchmarks against QuestPDF. A pull request merges when all of them are green. Mutation testing
is not run on pull requests; it runs over `main` every night, and a shard below its floor opens an issue.

Why the checks are what they are is recorded in the [architecture decision records](docs/adr/README.md), notably
[quality gates](docs/adr/0007-quality-gates.md) and [performance targets](docs/adr/0009-performance-targets.md).

## Dependencies

Package versions are set in [`Directory.Packages.props`](Directory.Packages.props) alone, and Dependabot proposes
updates weekly. Two pins are deliberate and explained where they are set: QuestPDF stays at 2026.5.0, its last MIT
release, as the tests' oracle — never upgrade it — and SkiaSharp and HarfBuzzSharp move together, checked against
the glibc and libfontconfig notes beside them.

## Reporting security issues

Do not open a public issue for a vulnerability. See [`SECURITY.md`](SECURITY.md).

## Licence

Contributions are made under the repository's [MIT licence](LICENSE).
