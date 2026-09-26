using System.Globalization;

namespace Rustaveli.Pdf.UnitTests.LineBreaking;

/// <summary>
/// The Unicode Character Database files under tests/assets/unicode. Read here independently of
/// eng/unicode-line-break.cs, so that the tests check the table it generated instead of repeating its work.
/// </summary>
internal static class UnicodeData
{
    public const int CodepointCount = 0x110000;

    public static string PathOf(string fileName) => Path.Combine(AppContext.BaseDirectory, "assets", "unicode", fileName);

    /// <summary>
    /// One value per code point: the one the file lists, else its <c>@missing</c> default, else
    /// <paramref name="fallback"/>.
    /// </summary>
    public static string[] Property(string fileName, string fallback)
    {
        string[] values = new string[CodepointCount];

        for (int codepoint = 0; codepoint < CodepointCount; codepoint++)
            values[codepoint] = fallback;

        foreach ((int first, int last, string value) in Ranges(fileName))
        {
            for (int codepoint = first; codepoint <= last; codepoint++)
                values[codepoint] = value;
        }

        return values;
    }

    /// <summary>Whether each code point has the binary property <paramref name="name"/>.</summary>
    public static bool[] BinaryProperty(string fileName, string name)
    {
        bool[] values = new bool[CodepointCount];

        foreach ((int first, int last, string value) in Ranges(fileName))
        {
            for (int codepoint = first; codepoint <= last; codepoint++)
                values[codepoint] |= value == name;
        }

        return values;
    }

    private static IEnumerable<(int First, int Last, string Value)> Ranges(string fileName)
    {
        const string Missing = "# @missing:";

        foreach (string line in File.ReadLines(PathOf(fileName)))
        {
            string content = line.StartsWith(Missing, StringComparison.Ordinal) ? line.Substring(Missing.Length) : line;
            int comment = content.IndexOf('#');
            content = (comment < 0 ? content : content.Substring(0, comment)).Trim();

            if (content.Length == 0)
                continue;

            string[] fields = content.Split(';');
            string range = fields[0].Trim();
            int dots = range.IndexOf("..", StringComparison.Ordinal);
            int first = Hex(dots < 0 ? range : range.Substring(0, dots));
            int last = dots < 0 ? first : Hex(range.Substring(dots + 2));

            yield return (first, last, fields[1].Trim());
        }
    }

    public static int Hex(string digits) => int.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
