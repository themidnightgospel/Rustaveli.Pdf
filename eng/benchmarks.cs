// Turns a benchmark run into numbers main's history keeps, and compares a pull request's with main's last and with
// QuestPDF's.
//
//     dotnet run eng/benchmarks.cs -- results <results folder> <sizes.json> <numbers.json> <questpdf.json>
//     dotnet run eng/benchmarks.cs -- compare <numbers.json> <questpdf.json> <main's data.js> <main.md> <questpdf.md>
//     dotnet run eng/benchmarks.cs -- questpdf <numbers.json> <questpdf.json> <latest.md> <light.svg> <dark.svg>
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
//
// questpdf writes that second comparison alone, for main: the page the README's Benchmarks link opens, which each
// merge's run replaces on the benchmark-data branch.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Measured = (double Time, double Deviation, double Allocated);
using Palette = (string Background, string Border, string Text, string Muted, string Accent, string Better, string Worse);

const double Limit = 0.10;
const string Parallel = "Eight documents in parallel";

// Why the benchmarks measure that release and no later one: from 2026.6.0 on, QuestPDF's licence forbids using it to
// develop or market a competing PDF library (docs/questpdf.md), so the pin in Directory.Packages.props never moves.
const string ReferenceNote = "the last MIT-licensed release";

return args.Length == 5 && args[0] == "results" ? Results(args[1], args[2], args[3], args[4])
    : args.Length == 6 && args[0] == "compare" ? Compare(args[1], args[2], args[3], args[4], args[5])
    : args.Length == 6 && args[0] == "questpdf" ? Latest(args[1], args[2], args[3], args[4], args[5])
    : Usage();

static int Usage()
{
    Console.Error.WriteLine("usage: results <results folder> <sizes.json> <numbers.json> <questpdf.json>");
    Console.Error.WriteLine("       compare <numbers.json> <questpdf.json> <data.js> <main.md> <questpdf.md>");
    Console.Error.WriteLine("       questpdf <numbers.json> <questpdf.json> <latest.md> <light.svg> <dark.svg>");
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
    string reference = Reference();

    void Both(string name, string unit, double mine, double quest, double? deviation)
    {
        string? range = deviation is double spread ? $"± {Number(spread)}" : null;
        string against = $"{reference}: {Number(quest)} {unit}, {Percent(mine, quest)}";
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
    AgainstQuest(ours, quest, source, null, questOutput);
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

// The comparison with QuestPDF for main as of its latest merge, which source's commit is.
static int Latest(string numbersFile, string questFile, string output, string light, string dark)
{
    List<(string Name, string Unit, double Value)> ours = Read(numbersFile);
    Dictionary<string, double> quest = Read(questFile).ToDictionary(entry => entry.Name, entry => entry.Value);
    string? source = Environment.GetEnvironmentVariable("BENCHMARK_SOURCE");
    string? commit = Environment.GetEnvironmentVariable("BENCHMARK_COMMIT");
    string date = DateTime.UtcNow.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    string measured = source is null || commit is null
        ? $"Measured on main on {date}."
        : $"Measured on main as of its latest merge, [{commit[..Math.Min(7, commit.Length)]}]({source.Replace("/blob/", "/commit/", StringComparison.Ordinal)}), "
          + $"on {date}, on a GitHub-hosted runner. Every merge's numbers are charted in the "
          + "[benchmark history](https://themidnightgospel.github.io/Rustaveli.Pdf/benchmarks/).";

    AgainstQuest(ours, quest, source, measured, output);

    // The same comparison as a picture, which the README shows: GitHub can show another file's picture, not its text.
    string at = commit is null ? "main" : $"main at {commit[..Math.Min(7, commit.Length)]}";
    File.WriteAllText(light, Picture(ours, quest, $"Measured on {at}, {date}", Light()));
    File.WriteAllText(dark, Picture(ours, quest, $"Measured on {at}, {date}", Dark()));
    return 0;
}

// A table drawn for each document: Rustaveli.Pdf's figures, QuestPDF's and the difference, in the colours of GitHub's
// light or dark theme, in the system's own sans-serif, which is all a picture shown on GitHub can use.
static string Picture(
    List<(string Name, string Unit, double Value)> ours, Dictionary<string, double> quest, string measured, Palette colours)
{
    const int Width = 880;
    const int Row = 26;
    const int Gap = 10;
    const int Top = 104;
    string[] figures = ["time", "allocated", "file size"];
    int[] columns = [520, 690, 856];

    List<IGrouping<string, (string Name, string Unit, double Value)>> documents = [.. ours.GroupBy(entry => Split(entry.Name).Document)];
    int height = Top + (documents.Count * 3 * Row) + ((documents.Count - 1) * Gap) + 46;
    StringBuilder svg = new StringBuilder();
    string reference = Reference();
    string title = $"Rustaveli.Pdf against {reference} ({ReferenceNote})";

    void Text(double x, double y, string text, string fill, string extra = "") =>
        svg.AppendLine(CultureInfo.InvariantCulture, $"""  <text x="{x}" y="{y}" fill="{fill}"{extra}>{Escape(text)}</text>""");

    svg.AppendLine(CultureInfo.InvariantCulture, $"""<svg xmlns="http://www.w3.org/2000/svg" width="{Width}" height="{height}" viewBox="0 0 {Width} {height}" role="img" aria-label="{Escape(title)}">""");
    svg.AppendLine("""  <style>text { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif; font-size: 13px; font-variant-numeric: tabular-nums; }</style>""");
    svg.AppendLine(CultureInfo.InvariantCulture, $"""  <rect x="0.5" y="0.5" width="{Width - 1}" height="{height - 1}" rx="6" fill="{colours.Background}" stroke="{colours.Border}"/>""");
    Text(24, 34, title, colours.Text, """ font-size="17" font-weight="600" """.TrimEnd());
    Text(24, 56, $"{measured} · the same documents, made by both libraries in the same run", colours.Muted);

    Text(24, 88, "Document", colours.Muted, """ font-weight="600" """.TrimEnd());

    for (int column = 0; column < figures.Length; column++)
        Text(columns[column], 88, Capitalised(figures[column]), colours.Muted, """ text-anchor="end" font-weight="600" """.TrimEnd());

    svg.AppendLine(CultureInfo.InvariantCulture, $"""  <line x1="24" y1="96" x2="{Width - 24}" y2="96" stroke="{colours.Border}"/>""");

    double y = Top + 18;

    foreach (IGrouping<string, (string Name, string Unit, double Value)> document in documents)
    {
        Dictionary<string, (string Name, string Unit, double Value)> byFigure =
            document.ToDictionary(entry => Split(entry.Name).Metric[3..], entry => entry);

        // A name too long for its column goes on over the next row.
        int wrap = document.Key.Length > 16 ? document.Key.LastIndexOf(' ', 16) : -1;

        if (wrap > 0)
        {
            Text(24, y, document.Key[..wrap], colours.Text, """ font-weight="600" """.TrimEnd());
            Text(24, y + Row, document.Key[(wrap + 1)..], colours.Text, """ font-weight="600" """.TrimEnd());
        }
        else
        {
            Text(24, y, document.Key, colours.Text, """ font-weight="600" """.TrimEnd());
        }
        Text(170, y, "Rustaveli.Pdf", colours.Accent, """ font-weight="600" """.TrimEnd());
        Text(170, y + Row, reference, colours.Text);
        Text(170, y + (2 * Row), "Difference", colours.Muted);

        for (int column = 0; column < figures.Length; column++)
        {
            if (!byFigure.TryGetValue(figures[column], out (string Name, string Unit, double Value) figure))
            {
                for (int line = 0; line < 3; line++)
                    Text(columns[column], y + (line * Row), "—", colours.Muted, """ text-anchor="end" """.TrimEnd());

                continue;
            }

            Text(columns[column], y, $"{Number(figure.Value)} {figure.Unit}", colours.Accent, """ text-anchor="end" font-weight="600" """.TrimEnd());

            if (quest.TryGetValue(figure.Name, out double theirs) && theirs > 0)
            {
                string fill = figure.Value <= theirs ? colours.Better : colours.Worse;
                Text(columns[column], y + Row, $"{Number(theirs)} {figure.Unit}", colours.Text, """ text-anchor="end" """.TrimEnd());
                Text(columns[column], y + (2 * Row), Percent(figure.Value, theirs), fill, """ text-anchor="end" font-weight="600" """.TrimEnd());
            }
        }

        y += (3 * Row) + Gap;

        if (document != documents[^1])
            svg.AppendLine(CultureInfo.InvariantCulture, $"""  <line x1="24" y1="{y - Row + 4 - (Gap / 2)}" x2="{Width - 24}" y2="{y - Row + 4 - (Gap / 2)}" stroke="{colours.Border}" stroke-dasharray="2 3"/>""");
    }

    Text(24, height - 18, "Smaller is better. Times come from a GitHub-hosted runner and vary by 10–20% between runs; allocations and file sizes do not.", colours.Muted, """ font-size="12" """.TrimEnd());
    svg.AppendLine("</svg>");
    return svg.ToString();
}

static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

static void AgainstQuest(
    List<(string Name, string Unit, double Value)> ours, Dictionary<string, double> quest, string? source, string? measured, string output)
{
    StringBuilder comment = new StringBuilder();
    string reference = Reference();

    comment.AppendLine("<!-- benchmarks-questpdf -->");
    comment.AppendLine($"### Benchmarks against {reference} ({ReferenceNote})");
    comment.AppendLine();

    if (measured is not null)
    {
        comment.AppendLine(measured);
        comment.AppendLine();
    }

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
        comment.AppendLine($"| {Linked(reference, theirs)} | {string.Join(" | ", figures.Select(figure => Theirs(figure, quest)))} |");
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

// The release the benchmarks measure, "QuestPDF 2026.5.0", read from its pin, so a comparison always names the
// release it was measured against.
static string Reference()
{
    for (DirectoryInfo? directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
    {
        string packages = Path.Combine(directory.FullName, "Directory.Packages.props");

        if (!File.Exists(Path.Combine(directory.FullName, "Rustaveli.Pdf.slnx")) || !File.Exists(packages))
            continue;

        string? version = XDocument.Load(packages).Descendants("PackageVersion")
            .FirstOrDefault(package => (string?)package.Attribute("Include") == "QuestPDF")?.Attribute("Version")?.Value;

        return version is null
            ? throw new InvalidOperationException("Directory.Packages.props pins no version of QuestPDF.")
            : $"QuestPDF {version}";
    }

    throw new InvalidOperationException("Run this from inside the Rustaveli.Pdf repository.");
}

// GitHub's light theme, the figures in the blue of the README's invoice.
static Palette Light() => ("#ffffff", "#d1d9e0", "#1f2328", "#59636e", "#1565c0", "#1a7f37", "#cf222e");

// GitHub's dark theme, the figures in the documentation site's gold.
static Palette Dark() => ("#0d1117", "#3d444d", "#f0f6fc", "#9198a1", "#e6b34a", "#3fb950", "#f85149");
