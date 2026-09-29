# Fuzzing

Coverage-guided fuzzing of every reader of outside files, with [libFuzzer](https://llvm.org/docs/LibFuzzer.html) and
[SharpFuzz](https://github.com/Metalnem/sharpfuzz). libFuzzer starts from a corpus of real files, mutates them, and
keeps each mutation that reaches branches of the instrumented library no input reached before. It stops at the first
input that fails.

| Target   | Reads                                          | Seeded from                      |
|----------|------------------------------------------------|----------------------------------|
| `fonts`  | TrueType, OpenType and collection files, then measures, kerns and subsets every face | `tests/assets/fonts` |
| `images` | PNG and JPEG files, then encodes them for PDF   | `tests/assets/images`            |
| `svg`    | SVG documents, then exports them as PDF         | `tests/assets/svg`               |
| `pdf`    | PDF files, then saves them plainly and linearised | files the library writes: `seed` |

An input fails when a reader throws anything it does not document, runs for more than ten seconds, or asks for more
memory than the capped heap holds. Files the library writes from what it read — font subsets, saved PDFs — must read
back without any exception at all.

[`.github/workflows/fuzz.yml`](../.github/workflows/fuzz.yml) fuzzes each target for two minutes on a pull request
that touches the library, and for twenty every night, keeping the corpus between runs. A failing input is uploaded
with the run as `fuzz-failure-<target>`.

## Replaying a failure

The fuzzing program replays an input without libFuzzer, against the library as built, which is how a failure is
debugged and how its fix is checked:

```bash
dotnet run --project fuzz/Rustaveli.Pdf.Fuzzing -c Release -- repro svg path/to/crash-file
```

It prints `handled` when the input is read, or rejected as the reader documents; otherwise the exception propagates.
The fix comes with a test that reads the input, or the smallest part of it that fails, in the suite for that reader.

## Running it locally

On Linux, with the libFuzzer driver from
[libfuzzer-dotnet's releases](https://github.com/Metalnem/libfuzzer-dotnet/releases) (Windows has one too):

```bash
dotnet tool restore
dotnet publish fuzz/Rustaveli.Pdf.Fuzzing -c Release -o artifacts/fuzz/out

# Seed before instrumenting: an instrumented library runs only under libFuzzer.
mkdir -p artifacts/fuzz/corpus/pdf
dotnet artifacts/fuzz/out/Rustaveli.Pdf.Fuzzing.dll seed artifacts/fuzz/corpus/pdf

dotnet sharpfuzz artifacts/fuzz/out/Rustaveli.Pdf.dll
dotnet sharpfuzz artifacts/fuzz/out/Rustaveli.Pdf.Operations.dll

FUZZ_TARGET=pdf DOTNET_GCHeapHardLimit=0x40000000 ./libfuzzer-dotnet \
  --target_path=dotnet --target_arg=artifacts/fuzz/out/Rustaveli.Pdf.Fuzzing.dll \
  -timeout=10 -max_total_time=300 artifacts/fuzz/corpus/pdf
```

The other targets are seeded by copying their files from `tests/assets` into their corpus folder.
