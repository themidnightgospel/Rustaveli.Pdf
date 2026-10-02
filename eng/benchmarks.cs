// Turns a benchmark run into numbers main's history keeps, and compares a pull request's with main's last.
//
//     dotnet run eng/benchmarks.cs -- results <results folder> <sizes.json> <out.json>
//     dotnet run eng/benchmarks.cs -- compare <out.json> <main's data.js> <comment.md>
//
// results reads BenchmarkDotNet's JSON reports and the sizes the benchmark project writes with --sizes, and writes
// each of this library's numbers — per document, its time, what it allocates and its file size, and the parallel
// time — in the form github-action-benchmark keeps (customSmallerIsBetter), each with the reference library's beside
// it. compare reads the last numbers from main the action kept and writes a table for the pull request; it fails when
// an allocation or a file size is more than 10% larger than main's (docs/adr/0009-performance-targets.md). Times
// are shown but not held to it: hosted runners differ by 10–20% from one run to the next.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Measured = (double Time, double Deviation, double Allocated);

const double Limit = 0.10;
const string Marker = "<!-- benchmarks -->";

return args.Length == 4 && args[0] == "results" ? Results(args[1], args[2], args[3])
    : args.Length == 4 && args[0] == "compare" ? Compare(args[1], args[2], args[3])
    : Usage();

static int Usage()
{
    Console.Error.WriteLine("usage: results <results folder> <sizes.json> <out.json> | compare <out.json> <data.js> <comment.md>");
    return 2;
}

static int Results(string folder, string sizesFile, string output)
{
    Dictionary<string, Measured> measured =
        new Dictionary<string, Measured>(StringComparer.Ordinal);

    foreach (string report in Directory.EnumerateFiles(folder, "*-report-full-compressed.json"))
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(report))!;

        foreach (JsonNode? benchmark in root["Benchmarks"]!.AsArray())
        {
            string method = (string)benchmark!["Method"]!;
            string parameters = (string?)benchmark["Parameters"] ?? string.Empty;
            string key = parameters.Length == 0 ? method : $"{method} {parameters}";

            measured[key] = (
                (double)benchmark["Statistics"]!["Mean"]! / 1e6,
                (double)benchmark["Statistics"]!["StandardDeviation"]! / 1e6,
                (double)benchmark["Memory"]!["BytesAllocatedPerOperation"]! / 1024);
        }
    }

    JsonObject sizes = JsonNode.Parse(File.ReadAllText(sizesFile))!.AsObject();
    JsonArray entries = [];

    foreach ((string document, JsonNode? size) in sizes)
    {
        if (measured.TryGetValue($"Rustaveli Kind={document}", out Measured ours)
            && measured.TryGetValue($"QuestPdf Kind={document}", out Measured theirs))
        {
            Add(entries, $"{document} · time", "ms", ours.Time, $"± {Number(ours.Deviation)}", Against(ours.Time, theirs.Time, "ms"));
            Add(entries, $"{document} · allocated", "KB", ours.Allocated, null, Against(ours.Allocated, theirs.Allocated, "KB"));
        }

        double mine = (int)size!["Rustaveli"]! / 1024.0;
        double reference = (int)size["Reference"]! / 1024.0;
        Add(entries, $"{document} · file size", "KB", mine, null, Against(mine, reference, "KB"));
    }

    if (measured.TryGetValue("RustaveliParallel", out Measured parallel)
        && measured.TryGetValue("QuestPdfParallel", out Measured theirParallel))
    {
        Add(entries, "Eight documents in parallel · time", "ms", parallel.Time, $"± {Number(parallel.Deviation)}", Against(parallel.Time, theirParallel.Time, "ms"));
    }

    File.WriteAllText(output, entries.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    Console.WriteLine($"{entries.Count} numbers written to {output}");
    return 0;
}

static void Add(JsonArray entries, string name, string unit, double value, string? range, string extra)
{
    JsonObject entry = new JsonObject
    {
        ["name"] = name,
        ["unit"] = unit,
        ["value"] = Math.Round(value, 3),
        ["extra"] = extra,
    };

    if (range is not null)
        entry["range"] = range;

    entries.Add((JsonNode)entry);
}

static int Compare(string currentFile, string baselineFile, string output)
{
    JsonArray current = JsonNode.Parse(File.ReadAllText(currentFile))!.AsArray();
    (Dictionary<string, double> main, string? commit) = Baseline(baselineFile);
    StringBuilder comment = new StringBuilder();
    List<string> over = [];

    comment.AppendLine(Marker);
    comment.AppendLine("### Benchmarks");
    comment.AppendLine();

    comment.AppendLine(commit is null
        ? "There are no numbers from main to compare with yet; these are this pull request's."
        : $"Compared with main at {commit[..Math.Min(7, commit.Length)]}. An allocation or a file size more than 10% larger "
          + "than main's fails this check; times are shown but not held to it, since hosted runners differ by 10–20% "
          + "from one run to the next.");

    comment.AppendLine();
    comment.AppendLine("| | main | this pull request | change | against the reference |");
    comment.AppendLine("|---|---:|---:|---:|---|");

    // Where the measured source can be read, as of the pull request's commit, so its lines stay where they point.
    string? source = Environment.GetEnvironmentVariable("BENCHMARK_SOURCE");

    foreach (JsonNode? entry in current)
    {
        string name = (string)entry!["name"]!;
        string unit = (string)entry["unit"]!;
        double value = (double)entry["value"]!;
        string reference = (string?)entry["extra"] ?? string.Empty;
        string before = "—";
        string change = "—";
        string label = name;

        // The document, or the parallel benchmark, links to the code that makes it, and the reference to its own.
        int split = name.IndexOf(" · ", StringComparison.Ordinal);

        if (source is not null && split > 0)
        {
            (string? ours, string? theirs) = Sources(source, name[..split]);

            if (ours is not null)
                label = $"[{name[..split]}]({ours}){name[split..]}";

            if (theirs is not null && reference.StartsWith("reference ", StringComparison.Ordinal))
                reference = $"[reference]({theirs}){reference["reference".Length..]}";
        }

        if (main.TryGetValue(name, out double previous) && previous > 0)
        {
            double ratio = (value - previous) / previous;
            bool held = !name.EndsWith("· time", StringComparison.Ordinal);
            before = $"{Number(previous)} {unit}";
            change = ratio.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture);

            if (ratio > Limit)
            {
                change = held ? $"**{change}** over the limit" : $"**{change}**";

                if (held)
                    over.Add(name);
            }
        }

        comment.AppendLine($"| {label} | {before} | {Number(value)} {unit} | {change} | {reference} |");
    }

    if (over.Count > 0)
    {
        comment.AppendLine();
        comment.AppendLine($"More than 10% larger than main's: {string.Join(", ", over)}.");
    }

    File.WriteAllText(output, comment.ToString());
    Console.WriteLine(comment.ToString());
    return over.Count == 0 ? 0 : 1;
}

// The code that makes a document, in this library and in the reference: its case in each library's documents, or,
// for the parallel benchmark, each library's benchmark method.
static (string? Ours, string? Theirs) Sources(string source, string document)
{
    const string Folder = "benchmarks/Rustaveli.Pdf.Benchmarks/";

    return document == "Eight documents in parallel"
        ? (Link(source, Folder + "ParallelBenchmarks.cs", "public int RustaveliParallel("),
           Link(source, Folder + "ParallelBenchmarks.cs", "public int QuestPdfParallel("))
        : (Link(source, Folder + "RustaveliDocuments.cs", $"case DocumentKind.{document}:"),
           Link(source, Folder + "QuestDocuments.cs", $"case DocumentKind.{document}:"));
}

// The address of the line holding the marker; for a case, of every line to the break that ends it.
static string? Link(string source, string path, string marker)
{
    if (!File.Exists(path))
        return null;

    string[] lines = File.ReadAllLines(path);
    int start = Array.FindIndex(lines, line => line.Contains(marker, StringComparison.Ordinal));

    if (start < 0)
        return null;

    int end = marker.StartsWith("case ", StringComparison.Ordinal)
        ? Array.FindIndex(lines, start, line => line.Trim() == "break;")
        : start;

    return end > start ? $"{source}/{path}#L{start + 1}-L{end + 1}" : $"{source}/{path}#L{start + 1}";
}

// The last numbers github-action-benchmark kept, from its data.js: `window.BENCHMARK_DATA = { ... }`.
static (Dictionary<string, double> Values, string? Commit) Baseline(string file)
{
    Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.Ordinal);

    if (!File.Exists(file))
        return (values, null);

    string text = File.ReadAllText(file);
    int start = text.IndexOf('{', StringComparison.Ordinal);

    if (start < 0)
        return (values, null);

    JsonNode data = JsonNode.Parse(text[start..])!;
    JsonArray? runs = data["entries"]?["Rustaveli.Pdf"]?.AsArray();

    if (runs is null || runs.Count == 0)
        return (values, null);

    JsonNode last = runs[^1]!;

    foreach (JsonNode? bench in last["benches"]!.AsArray())
        values[(string)bench!["name"]!] = (double)bench["value"]!;

    return (values, (string?)last["commit"]?["id"]);
}

static string Against(double ours, double theirs, string unit) =>
    $"reference {Number(theirs)} {unit}, {(ours / theirs).ToString("0.00", CultureInfo.InvariantCulture)}×";

static string Number(double value) =>
    value.ToString(value >= 100 ? "#,0" : value >= 10 ? "#,0.0" : "#,0.00", CultureInfo.InvariantCulture);
