namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>
/// The Unicode Character Database files under tests/assets/unicode, and the pieces of their shared format.
/// </summary>
internal static class UnicodeFiles
{
    public static string PathOf(string fileName) => Path.Combine(AppContext.BaseDirectory, "assets", "unicode", fileName);

    /// <summary>The fields of each data line of a file, without comments or blank lines.</summary>
    public static IEnumerable<string[]> DataLines(string fileName)
    {
        foreach (string line in File.ReadLines(PathOf(fileName)))
        {
            int comment = line.IndexOf('#');
            string content = (comment < 0 ? line : line.Substring(0, comment)).Trim();
            if (content.Length > 0)
                yield return content.Split(';');
        }
    }

    public static int Hex(string value) =>
        int.Parse(value.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A code point range as the files write it, "0041" or "0041..005A".</summary>
    public static (int First, int Last) Range(string field)
    {
        string[] ends = field.Trim().Split(new[] { ".." }, StringSplitOptions.None);
        int first = Hex(ends[0]);
        return (first, ends.Length == 2 ? Hex(ends[1]) : first);
    }
}
