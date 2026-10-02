// Turns a benchmark run into numbers main's history keeps, and compares a pull request's with main's last and with
// QuestPDF's.
//
//     dotnet run eng/benchmarks.cs -- results <results folder> <sizes.json> <numbers.json> <questpdf.json>
//     dotnet run eng/benchmarks.cs -- compare <numbers.json> <questpdf.json> <main's data.js> <main.md> <questpdf.md>
//
// results reads BenchmarkDotNet's JSON reports and the sizes the benchmark project writes with --sizes. It writes this
// library's numbers — per document, its time, what it allocates and its file size, and the parallel time — in the form
// github-action-benchmark keeps (customSmallerIsBetter), with QuestPDF's beside each for the charts' details, and
// QuestPDF's numbers under the same names on their own.
//
// compare writes two comments for the pull request. The first sets its numbers against the last that main kept, and
// the command fails when an allocation or a file size is more than 10% larger than main's
// (docs/adr/0009-performance-targets.md); times are shown but not held to it, since hosted runners differ by 10–20%
// from one run to the next. The second sets them against QuestPDF's from the same run, a table for each document.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Measured = (double Time, double Deviation, double Allocated);

const double Limit = 0.10;
const string Parallel = "Eight documents in parallel";

return args.Length == 5 && args[0] == "results" ? Results(args[1], args[2], args[3], args[4])
    : args.Length == 6 && args[0] == "compare" ? Compare(args[1], args[2], args[3], args[4], args[5])
    : Usage();

static int Usage()
{
    Console.Error.WriteLine("usage: results <results folder> <sizes.json> <numbers.json> <questpdf.json>");
    Console.Error.WriteLine("       compare <numbers.json> <questpdf.json> <data.js> <main.md> <questpdf.md>");
    return 2;
}

static int Results(string folder, string sizesFile, string output, string questOutput)
{
    Dictionary<string, Measured> measured = new Dictionary<string, Measured>(StringComparer.Ordinal);

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
    JsonArray ours = [];
    JsonArray theirs = [];

    void Both(string name, string unit, double mine, double quest, double? deviation)
    {
        string? range = deviation is double spread ? $"± {Number(spread)}" : null;
        string against = $"QuestPDF {Number(quest)} {unit}, {Percent(mine, quest)}";
        ours.Add((JsonNode)Entry(name, unit, mine, range, against));
        theirs.Add((JsonNode)Entry(name, unit, quest, null, null));
    }

    foreach ((string document, JsonNode? size) in sizes)
    {
        if (measured.TryGetValue($"Rustaveli Kind={document}", out Measured mine)
            && measured.TryGetValue($"QuestPdf Kind={document}", out Measured quest))
        {
            Both($"{document} · time", "ms", mine.Time, quest.Time, mine.Deviation);
            Both($"{document} · allocated", "KB", mine.Allocated, quest.Allocated, null);
        }

        Both($"{document} · file size", "KB", (int)size!["Rustaveli"]! / 1024.0, (int)size["Reference"]! / 1024.0, null);
    }

    if (measured.TryGetValue("RustaveliParallel", out Measured parallel)
        && measured.TryGetValue("QuestPdfParallel", out Measured questParallel))
    {
        Both($"{Parallel} · time", "ms", parallel.Time, questParallel.Time, parallel.Deviation);
    }

    JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    File.WriteAllText(output, ours.ToJsonString(options));
    File.WriteAllText(questOutput, theirs.ToJsonString(options));
    Console.WriteLine($"{ours.Count} numbers written to {output}, and QuestPDF's to {questOutput}");
    return 0;
}

static JsonObject Entry(string name, string unit, double value, string? range, string? extra)
{
    JsonObject entry = new JsonObject
    {
        ["name"] = name,
        ["unit"] = unit,
        ["value"] = Math.Round(value, 3),
    };

    if (extra is not null)
        entry["extra"] = extra;

    if (range is not null)
        entry["range"] = range;

    return entry;
}

static int Compare(string numbersFile, string questFile, string baselineFile, string mainOutput, string questOutput)
{
    List<(string Name, string Unit, double Value)> ours = Read(numbersFile);
    Dictionary<string, double> quest = ours.Count == 0 ? [] : Read(questFile).ToDictionary(entry => entry.Name, entry => entry.Value);
    (Dictionary<string, double> main, string? commit) = Baseline(baselineFile);

    // Where the measured source can be read, as of the pull request's commit, so its lines stay where they point.
    string? source = Environment.GetEnvironmentVariable("BENCHMARK_SOURCE");

    int status = AgainstMain(ours, main, commit, source, mainOutput);
    AgainstQuest(ours, quest, source, questOutput);
    return status;
}

static int AgainstMain(
    List<(string Name, string Unit, double Value)> ours, Dictionary<string, double> main, string? commit, string? source, string output)
{
    StringBuilder comment = new StringBuilder();
    List<string> over = [];

    comment.AppendLine("<!-- benchmarks -->");
    comment.AppendLine("### Benchmarks against main");
    comment.AppendLine();

    comment.AppendLine(commit is null
        ? "There are no numbers from main to compare with yet; these are this pull request's."
        : $"Compared with main at {commit[..Math.Min(7, commit.Length)]}. An allocation or a file size more than 10% larger "
          + "than main's fails this check; times are shown but not held to it, since hosted runners differ by 10–20% "
          + "from one run to the next.");

    comment.AppendLine();
    comment.AppendLine("| | main | this pull request | change |");
    comment.AppendLine("|---|---:|---:|---:|");

    foreach ((string name, string unit, double value) in ours)
    {
        string before = "—";
        string change = "—";

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

        // The document, or the parallel benchmark, links to the code that makes it.
        (string document, string metric) = Split(name);
        string? code = source is null ? null : Sources(source, document).Ours;
        string label = code is null ? name : $"[{document}]({code}){metric}";

        comment.AppendLine($"| {label} | {before} | {Number(value)} {unit} | {change} |");
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

static void AgainstQuest(
    List<(string Name, string Unit, double Value)> ours, Dictionary<string, double> quest, string? source, string output)
{
    StringBuilder comment = new StringBuilder();

    comment.AppendLine("<!-- benchmarks-questpdf -->");
    comment.AppendLine("### Benchmarks against QuestPDF");
    comment.AppendLine();
    comment.AppendLine("Both libraries make the same documents in the same run. For each figure smaller is better; the "
        + "difference is how much smaller (−) or larger (+) Rustaveli.Pdf's figure is than QuestPDF's.");

    // A table for each document, in the order it was measured, with a column for each of its figures.
    foreach (IGrouping<string, (string Name, string Unit, double Value)> document in ours.GroupBy(entry => Split(entry.Name).Document))
    {
        (string? mine, string? theirs) = source is null ? (null, null) : Sources(source, document.Key);
        List<(string Name, string Unit, double Value)> figures = [.. document];

        comment.AppendLine();
        comment.AppendLine($"#### {document.Key}");
        comment.AppendLine();
        comment.AppendLine($"| | {string.Join(" | ", figures.Select(figure => Capitalised(Split(figure.Name).Metric[3..])))} |");
        comment.AppendLine($"|---|{string.Concat(figures.Select(_ => "---:|"))}");
        comment.AppendLine($"| {Linked("Rustaveli.Pdf", mine)} | {string.Join(" | ", figures.Select(figure => $"{Number(figure.Value)} {figure.Unit}"))} |");
        comment.AppendLine($"| {Linked("QuestPDF", theirs)} | {string.Join(" | ", figures.Select(figure => Theirs(figure, quest)))} |");
        comment.AppendLine($"| Difference | {string.Join(" | ", figures.Select(figure => Difference(figure, quest)))} |");
    }

    File.WriteAllText(output, comment.ToString());
    Console.WriteLine(comment.ToString());
}

static string Theirs((string Name, string Unit, double Value) figure, Dictionary<string, double> quest) =>
    quest.TryGetValue(figure.Name, out double value) ? $"{Number(value)} {figure.Unit}" : "—";

static string Difference((string Name, string Unit, double Value) figure, Dictionary<string, double> quest) =>
    quest.TryGetValue(figure.Name, out double value) && value > 0 ? Percent(figure.Value, value) : "—";

static string Linked(string text, string? address) => address is null ? text : $"[{text}]({address})";

static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

// "Invoice · time" as its document, "Invoice", and its figure, " · time".
static (string Document, string Metric) Split(string name)
{
    int split = name.IndexOf(" · ", StringComparison.Ordinal);
    return split < 0 ? (name, string.Empty) : (name[..split], name[split..]);
}

static List<(string Name, string Unit, double Value)> Read(string file) =>
    File.Exists(file)
        ? JsonNode.Parse(File.ReadAllText(file))!.AsArray()
            .Select(entry => ((string)entry!["name"]!, (string)entry["unit"]!, (double)entry["value"]!))
            .ToList()
        : [];

// The code that makes a document, in this library and in QuestPDF: its case in each library's documents, or, for the
// parallel benchmark, each library's benchmark method.
static (string? Ours, string? Theirs) Sources(string source, string document)
{
    const string Folder = "benchmarks/Rustaveli.Pdf.Benchmarks/";

    return document == Parallel
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

// How much smaller (−) or larger (+) ours is than theirs, to the whole percent.
static string Percent(double ours, double theirs) =>
    ((ours - theirs) / theirs).ToString("+0%;−0%;0%", CultureInfo.InvariantCulture);

static string Number(double value) =>
    value.ToString(value >= 100 ? "#,0" : value >= 10 ? "#,0.0" : "#,0.00", CultureInfo.InvariantCulture);
