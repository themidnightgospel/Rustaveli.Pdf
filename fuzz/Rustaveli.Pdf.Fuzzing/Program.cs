using Rustaveli.Pdf.Fuzzing;
using SharpFuzz;

// dotnet Rustaveli.Pdf.Fuzzing.dll                      under libFuzzer: fuzz the target FUZZ_TARGET names
// dotnet Rustaveli.Pdf.Fuzzing.dll repro <target> <file>  run one input through a target, as a crash is reproduced
// dotnet Rustaveli.Pdf.Fuzzing.dll seed <folder>          write the PDF files the pdf target starts from
//
// The target is named in the environment rather than on the command line, as libFuzzer's .NET driver passes the
// program a single argument, its path.
if (args is ["seed", string folder])
{
    PdfSeeds.Write(folder);
    return;
}

if (args is ["repro", string name, string file])
{
    Targets.Named(name)(File.ReadAllBytes(file));
    Console.WriteLine($"{file}: handled");
    return;
}

string target = Environment.GetEnvironmentVariable("FUZZ_TARGET")
    ?? throw new InvalidOperationException("Name the target to fuzz in FUZZ_TARGET: " + string.Join(", ", Targets.Names) + ".");

ReadOnlySpanAction run = Targets.Named(target);
Fuzzer.LibFuzzer.Run(run);
