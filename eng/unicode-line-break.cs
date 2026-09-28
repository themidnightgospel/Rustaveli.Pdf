// Generates the Line_Break table the line breaker reads, from the Unicode Character Database files committed under
// tests/assets/unicode.
//
//     dotnet run eng/unicode-line-break.cs
//
// Writes src/Rustaveli.Pdf/Text/LineBreaking/LineBreakTable.cs. To move to a newer Unicode version, replace the data
// files (tests/assets/unicode/README.md lists where they come from), rerun this, and then read that version of UAX #14
// against LineBreakEnumerator: new versions change the rules about as often as they change the data.
//
// Each code point becomes one byte: the class as LineBreakClass numbers it in the low six bits, then a flag for East
// Asian width F, W or H, then a flag for an unassigned Extended_Pictographic code point. The bytes are stored as a
// three-level trie — a top index by 4096 code points, a middle index by 128, and 128-byte leaves, all deduplicated —
// which holds the 1.1 million code points in about 28 KB with every index fitting in a byte.

using System.Globalization;
using System.Text;

const string Version = "16.0.0";
const int CodepointCount = 0x110000;
const int TopShift = 12;
const int MiddleShift = 7;
const int LeafSize = 1 << MiddleShift;
const int MiddleSize = 1 << (TopShift - MiddleShift);
const int EastAsianFlag = 0x40;
const int UnassignedPictographicFlag = 0x80;

// In LineBreakClass order. Sot and Eot are positions rather than property values and never occur in the data.
string[] classes =
[
    "Sot", "Eot", "BK", "CR", "LF", "NL", "SP", "ZW", "CM", "ZWJ", "WJ", "GL", "CB", "AK", "AL", "AP", "AS", "B2", "BA",
    "BB", "CL", "CP", "EB", "EM", "EX", "H2", "H3", "HL", "HY", "ID", "IN", "IS", "JL", "JT", "JV", "NS", "NU", "OP",
    "PO", "PR", "QU", "QUPi", "QUPf", "RI", "SY", "VF", "VI"
];

string root = RepositoryRoot();
string data = Path.Combine(root, "tests", "assets", "unicode");
string output = Path.Combine(root, "src", "Rustaveli.Pdf", "Text", "LineBreaking", "LineBreakTable.cs");

string[] lineBreak = Read("LineBreak.txt", $"LineBreak-{Version}.txt");
string[] eastAsianWidth = Read("EastAsianWidth.txt", $"EastAsianWidth-{Version}.txt");
string[] generalCategory = Read("DerivedGeneralCategory.txt", $"DerivedGeneralCategory-{Version}.txt");
string[] emoji = Read("emoji-data.txt", "Emoji Version 16.0", property: "Extended_Pictographic");

byte[] values = new byte[CodepointCount];

for (int codepoint = 0; codepoint < CodepointCount; codepoint++)
{
    string category = generalCategory[codepoint];
    string resolved = lineBreak[codepoint] switch
    {
        // LB1, for text of unknown language: ambiguous, surrogate and unknown characters are alphabetic, a complex
        // context character is a combining mark when it is one and alphabetic otherwise, and a conditional Japanese
        // starter is a nonstarter.
        "AI" or "SG" or "XX" => "AL",
        "SA" => category is "Mn" or "Mc" ? "CM" : "AL",
        "CJ" => "NS",
        "QU" when category == "Pi" => "QUPi",
        "QU" when category == "Pf" => "QUPf",
        string value => value
    };

    int index = Array.IndexOf(classes, resolved);

    if (index < 2)
        throw new InvalidDataException($"U+{codepoint:X4} has Line_Break={resolved}, which LineBreakClass lacks.");

    if (eastAsianWidth[codepoint] is "F" or "W" or "H")
        index |= EastAsianFlag;

    if (emoji[codepoint] == "Extended_Pictographic" && category == "Cn")
        index |= UnassignedPictographicFlag;

    values[codepoint] = (byte)index;
}

List<byte> leaves = [];
Dictionary<string, int> leafIndex = [];
int[] leafOf = new int[CodepointCount / LeafSize];

for (int block = 0; block < leafOf.Length; block++)
    leafOf[block] = Intern(values.AsSpan(block * LeafSize, LeafSize), leaves, leafIndex, LeafSize);

List<byte> middle = [];
Dictionary<string, int> middleIndex = [];
byte[] top = new byte[CodepointCount >> TopShift];

for (int chunk = 0; chunk < top.Length; chunk++)
{
    byte[] entries = leafOf.Skip(chunk * MiddleSize).Take(MiddleSize).Select(Byte).ToArray();
    top[chunk] = Byte(Intern(entries, middle, middleIndex, MiddleSize));
}

// Leaves are interned in code point order, so the leading blocks that are all distinct sit at their own index: below
// this limit the leaves can be read by code point directly, which is what makes Latin text a single array read.
int directLimit = 0;
while (directLimit < leafOf.Length && leafOf[directLimit] == directLimit)
    directLimit++;

directLimit *= LeafSize;

if (directLimit < 256)
    throw new InvalidDataException("Latin-1 is no longer directly indexable; the fast path needs rethinking.");

for (int codepoint = 0; codepoint < CodepointCount; codepoint++)
{
    int leaf = middle[(top[codepoint >> TopShift] << (TopShift - MiddleShift)) | ((codepoint >> MiddleShift) & (MiddleSize - 1))];

    if (leaves[(leaf << MiddleShift) | (codepoint & (LeafSize - 1))] != values[codepoint])
        throw new InvalidOperationException($"The trie reads U+{codepoint:X4} back wrongly.");
}

StringBuilder source = new StringBuilder();
source.Append($$"""
    // <auto-generated>
    //     Generated by eng/unicode-line-break.cs from the Unicode {{Version}} character database; rerun it rather than
    //     editing this file.
    // </auto-generated>
    //
    // Derived from Unicode data files, copyright Unicode, Inc., and distributed under the Unicode License v3; the
    // licence and the files' sources are in tests/assets/unicode/README.md.

    namespace Rustaveli.Pdf.Text.LineBreaking;

    /// <summary>
    /// The resolved Line_Break class of every code point, with the East Asian width and unassigned pictographic flags,
    /// as a three-level trie. <see cref="LineBreakProperties.Of"/> reads it.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("eng/unicode-line-break.cs", "{{Version}}")]
    internal static class LineBreakTable
    {
        public const string UnicodeVersion = "{{Version}}";

        /// <summary>Code points below this read their leaf directly, without going through the indexes.</summary>
        public const int DirectLimit = 0x{{directLimit:X}};

        public const int TopShift = {{TopShift}};
        public const int MiddleShift = {{MiddleShift}};
        public const int MiddleMask = 0x{{MiddleSize - 1:X}};
        public const int LeafMask = 0x{{LeafSize - 1:X}};


    """);

AppendArray(source, "Top", "The middle chunk for each 4096 code points.", top);
source.AppendLine();
AppendArray(source, "Middle", "The leaf for each 128 code points, in chunks of 32.", middle);
source.AppendLine();
AppendArray(source, "Leaves", "The value of each code point, in leaves of 128.", leaves);
source.AppendLine("}");

File.WriteAllText(output, source.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
Console.WriteLine($"wrote {output}");
Console.WriteLine($"  top {top.Length} B, middle {middle.Count} B, leaves {leaves.Count} B, direct below U+{directLimit:X4}");
return 0;

// Parses a UCD file into one value per code point. Code points the file does not list take its @missing default;
// with a property name, the file lists that binary property, listed code points get the name and the rest nothing.
// The version marker is text the file's header must contain, so that a stray file from another version fails loudly.
string[] Read(string fileName, string versionMarker, string? property = null)
{
    string path = Path.Combine(data, fileName);
    string[] result = new string[CodepointCount];
    bool versionSeen = false;

    foreach (string line in File.ReadLines(path))
    {
        versionSeen |= line.StartsWith('#') && line.Contains(versionMarker, StringComparison.Ordinal);

        const string Missing = "# @missing:";
        string content = line.StartsWith(Missing, StringComparison.Ordinal) ? line[Missing.Length..] : line;
        int comment = content.IndexOf('#');
        content = (comment < 0 ? content : content[..comment]).Trim();

        if (content.Length == 0)
            continue;

        string[] fields = content.Split(';', StringSplitOptions.TrimEntries);

        if (property != null && fields[1] != property)
            continue;

        string[] range = fields[0].Split("..");
        int first = int.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int last = range.Length == 1 ? first : int.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        Array.Fill(result, fields[1], first, last - first + 1);
    }

    if (!versionSeen)
        throw new InvalidDataException($"{fileName} is not the version this table is for: no '{versionMarker}'.");

    if (property != null)
        return Array.ConvertAll(result, value => value ?? "");

    int unset = Array.IndexOf(result, null);
    if (unset >= 0)
        throw new InvalidDataException($"{fileName} gives no value, not even a default, for U+{unset:X4}.");

    return result;
}

static int Intern(ReadOnlySpan<byte> block, List<byte> store, Dictionary<string, int> index, int size)
{
    string key = Convert.ToHexString(block);

    if (!index.TryGetValue(key, out int position))
    {
        position = store.Count / size;
        index.Add(key, position);
        store.AddRange(block.ToArray());
    }

    return position;
}

static byte Byte(int value) =>
    value <= byte.MaxValue ? (byte)value : throw new InvalidDataException("An index no longer fits in a byte.");

static void AppendArray(StringBuilder source, string name, string summary, IReadOnlyList<byte> bytes)
{
    source.AppendLine($"    /// <summary>{summary}</summary>");
    source.AppendLine($"    public static ReadOnlySpan<byte> {name} => new byte[]");
    source.AppendLine("    {");

    StringBuilder line = new StringBuilder("       ");

    for (int index = 0; index < bytes.Count; index++)
    {
        string item = $" {bytes[index]},";

        if (line.Length + item.Length > 120)
        {
            source.AppendLine(line.ToString());
            line.Clear().Append("       ");
        }

        line.Append(item);
    }

    source.AppendLine(line.ToString().TrimEnd(','));
    source.AppendLine("    };");
}

// Found by walking up from the working directory, not from this script's location: a file-based app's build output
// is cached and can be shared between checkouts, so a path captured at compile time may name a different one.
static string RepositoryRoot()
{
    for (DirectoryInfo? directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Rustaveli.Pdf.slnx")))
            return directory.FullName;
    }

    throw new InvalidOperationException("Run this from inside the Rustaveli.Pdf repository.");
}
