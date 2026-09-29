using System.Diagnostics;
using System.Xml.Linq;

namespace Rustaveli.Pdf.ConformanceTests.Validation;

/// <summary>
/// Runs veraPDF, the reference validator for PDF/A and PDF/UA, over files claiming either: each is checked against every
/// part and level its metadata claims, rule by rule.
/// </summary>
internal static class VeraPdf
{
    private static readonly Lazy<(string Java, string Home)> Installation = new Lazy<(string, string)>(Locate);

    /// <summary>
    /// Validates every file in one run — Java starts once — and returns, for each by name, the rules it broke; an empty
    /// list is a file that complies with everything it claims.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(IReadOnlyDictionary<string, byte[]> files)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"verapdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            foreach (KeyValuePair<string, byte[]> file in files)
                File.WriteAllBytes(Path.Combine(directory, file.Key + ".pdf"), file.Value);

            (string java, string home) = Installation.Value;
            ProcessStartInfo start = new ProcessStartInfo(java)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            start.ArgumentList.Add("-classpath");
            start.ArgumentList.Add(Path.Combine(home, "etc") + Path.PathSeparator + Path.Combine(home, "bin", "*"));
            start.ArgumentList.Add("-Dfile.encoding=UTF8");

            // Without a home of its own, veraPDF keeps its configuration in ~/.verapdf, and on a machine that has none
            // yet, runs from tests in parallel race to create it: one fails that app.xml "must be a creatable or
            // readable file". Each run gets its own, in its own directory.
            start.ArgumentList.Add("-Dapp.home=" + directory);
            start.ArgumentList.Add("org.verapdf.apps.GreenfieldCliWrapper");
            start.ArgumentList.Add("--format");
            start.ArgumentList.Add("mrr");

            foreach (string file in files.Keys)
                start.ArgumentList.Add(Path.Combine(directory, file + ".pdf"));

            using Process process = Process.Start(start)!;
            Task<string> errors = process.StandardError.ReadToEndAsync();
            string report = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // No report at all is veraPDF itself failing, not a file failing it: say what veraPDF said.
            if (string.IsNullOrWhiteSpace(report))
                throw new InvalidOperationException($"veraPDF wrote no report and exited with {process.ExitCode}:\n{errors.Result}");

            return Failures(XDocument.Parse(report), errors.Result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Each file's broken rules, from the machine-readable report: clause, test and what the rule asks.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Failures(XDocument report, string errors)
    {
        Dictionary<string, IReadOnlyList<string>> failures = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (XElement job in report.Descendants("job"))
        {
            string name = Path.GetFileNameWithoutExtension(job.Element("item")?.Element("name")?.Value ?? string.Empty);
            List<string> broken = [];

            IReadOnlyList<XElement> reports = job.Descendants("validationReport").ToList();

            if (reports.Count == 0)
                broken.Add("veraPDF produced no validation report: " + (job.Element("taskException")?.Value ?? errors));

            foreach (XElement validation in reports)
            {
                string profile = validation.Attribute("profileName")?.Value ?? "?";

                foreach (XElement rule in validation.Descendants("rule").Where(rule => rule.Attribute("status")?.Value == "failed"))
                {
                    broken.Add(
                        $"{profile} {rule.Attribute("clause")?.Value}-{rule.Attribute("testNumber")?.Value}: " +
                        $"{rule.Element("description")?.Value} ({rule.Attribute("failedChecks")?.Value} failed)");
                }
            }

            failures[name] = broken;
        }

        return failures;
    }

    private static (string Java, string Home) Locate()
    {
        string home = Environment.GetEnvironmentVariable("VERAPDF") is { Length: > 0 } configured && Directory.Exists(configured)
            ? configured
            : Directory.Exists(RepositoryPaths.Tools)
                ? Directory.EnumerateDirectories(RepositoryPaths.Tools, "verapdf-*").FirstOrDefault(directory => Directory.Exists(Path.Combine(directory, "bin"))) ?? string.Empty
                : string.Empty;

        if (home.Length == 0)
        {
            throw new InvalidOperationException(
                "veraPDF was not found. Run `dotnet run eng/tools.cs` to fetch it, or set VERAPDF to where it is installed.");
        }

        return (Java(), home);
    }

    /// <summary>The Java fetched beside the tools, else JAVA_HOME's, else the first on PATH.</summary>
    private static string Java()
    {
        string name = OperatingSystem.IsWindows() ? "java.exe" : "java";

        string? fetched = Directory.Exists(RepositoryPaths.Tools)
            ? Directory.EnumerateFiles(RepositoryPaths.Tools, name, SearchOption.AllDirectories).FirstOrDefault()
            : null;

        if (fetched is not null)
            return fetched;

        string? home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, "bin", name)))
            return Path.Combine(home, "bin", name);

        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("veraPDF needs Java: install a Java runtime, 11 or later, or set JAVA_HOME.");
    }
}
