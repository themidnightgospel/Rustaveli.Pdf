using System.Diagnostics;

namespace Rustaveli.Pdf.ConformanceTests.Validation;

/// <summary>
/// Runs qpdf's structural check. qpdf parses every object, stream and cross-reference entry strictly, where viewers
/// silently repair damage — a file can open everywhere and still be wrong.
/// </summary>
internal static class Qpdf
{
    private static readonly Lazy<string> Executable = new Lazy<string>(Locate);

    /// <summary>Returns qpdf's report if the file is clean, and throws with that report if it is not.</summary>
    public static string Check(byte[] pdf)
    {
        string file = Path.Combine(Path.GetTempPath(), $"conformance-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(file, pdf);

        try
        {
            ProcessStartInfo start = new ProcessStartInfo(Executable.Value)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--check");
            start.ArgumentList.Add(file);

            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 0 is clean. 3 is "succeeded with warnings", which is still a defect in a file we wrote ourselves.
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"qpdf --check failed with exit code {process.ExitCode}:\n{output}");

            return output;
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Runs qpdf with <paramref name="arguments"/> around an input file holding <paramref name="pdf"/> and an output file,
    /// written in their places as <c>{input}</c> and <c>{output}</c>; returns its exit code, what it printed, and the
    /// output file if it wrote one.
    /// </summary>
    public static (int ExitCode, string Output, byte[]? File) Run(byte[] pdf, params string[] arguments)
    {
        string input = Path.Combine(Path.GetTempPath(), $"qpdf-in-{Guid.NewGuid():N}.pdf");
        string output = Path.Combine(Path.GetTempPath(), $"qpdf-out-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(input, pdf);

        try
        {
            ProcessStartInfo start = new ProcessStartInfo(Executable.Value)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (string argument in arguments)
                start.ArgumentList.Add(argument.Replace("{input}", input, StringComparison.Ordinal).Replace("{output}", output, StringComparison.Ordinal));

            using Process process = Process.Start(start)!;
            string printed = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, printed, File.Exists(output) ? File.ReadAllBytes(output) : null);
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    private static string Locate()
    {
        string name = OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf";

        string? configured = Environment.GetEnvironmentVariable("QPDF");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;

        if (Directory.Exists(RepositoryPaths.Tools))
        {
            string? fetched = Directory.EnumerateFiles(RepositoryPaths.Tools, name, SearchOption.AllDirectories).FirstOrDefault();
            if (fetched != null)
                return fetched;
        }

        string? onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);

        return onPath ?? throw new InvalidOperationException(
            "qpdf was not found. Run `dotnet run eng/tools.cs` to fetch it, install it on PATH, or set QPDF to its path.");
    }
}
