// Runs both test suites with coverage, merges the results and enforces the thresholds.
//
//     dotnet run eng/coverage.cs                 run, report, enforce
//     dotnet run eng/coverage.cs -- --no-test    re-check the last run without re-running the tests
//
// Thresholds live in eng/coverage-thresholds.json so that raising them is a reviewed one-line change. The merged
// HTML report is written to artifacts/coverage/report/index.html.

using System.Diagnostics;
using System.Text.Json;

string root = Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", ".."));
string results = Path.Combine(root, "artifacts", "coverage");
string report = Path.Combine(results, "report");
bool runTests = !args.Contains("--no-test");

if (runTests)
{
    if (Directory.Exists(results))
        Directory.Delete(results, recursive: true);

    // net10.0 only: coverage describes the source, and the source is the same for both targets. The net48 leg runs
    // in the ordinary test job.
    Run("dotnet", $"test \"{Path.Combine(root, "Rustaveli.Pdf.slnx")}\" -c Release -f net10.0 " +
                  $"--settings \"{Path.Combine(root, "coverage.runsettings")}\" --collect \"XPlat Code Coverage\" " +
                  $"--results-directory \"{results}\" -nologo");
}

Run("dotnet", $"reportgenerator \"-reports:{results}/**/coverage.cobertura.xml\" \"-targetdir:{report}\" " +
              "-reporttypes:Html;JsonSummary;TextSummary;MarkdownSummaryGithub");

using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(report, "Summary.json")));
JsonElement totals = summary.RootElement.GetProperty("summary");
double line = totals.GetProperty("linecoverage").GetDouble();
double branch = totals.GetProperty("branchcoverage").GetDouble();

using JsonDocument thresholds = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eng", "coverage-thresholds.json")));
double minLine = thresholds.RootElement.GetProperty("line").GetDouble();
double minBranch = thresholds.RootElement.GetProperty("branch").GetDouble();

Console.WriteLine();
Console.WriteLine($"line   coverage {line,6:F1}%   (minimum {minLine}%)");
Console.WriteLine($"branch coverage {branch,6:F1}%   (minimum {minBranch}%)");
Console.WriteLine($"report          {Path.Combine(report, "index.html")}");

bool passed = line >= minLine && branch >= minBranch;
Console.WriteLine(passed ? "coverage gate: PASSED" : "coverage gate: FAILED");
return passed ? 0 : 1;

static void Run(string file, string arguments)
{
    using Process process = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false })
        ?? throw new InvalidOperationException($"Could not start {file}.");
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine($"'{file} {arguments}' exited with {process.ExitCode}.");
        Environment.Exit(process.ExitCode);
    }
}
