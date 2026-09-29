using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Builds PDF files by hand, object by object, with a cross-reference table whose offsets are right — or, for damage,
/// deliberately wrong — and updates appended the way incremental saving appends them.
/// </summary>
internal sealed class HandmadePdf
{
    private readonly StringBuilder _file = new StringBuilder("%PDF-1.7\n");
    private readonly SortedDictionary<int, int> _offsets = [];
    private int _lastSection = -1;

    /// <summary>
    /// Appends object <paramref name="number"/>, of generation <paramref name="generation"/>, with
    /// <paramref name="body"/> between <c>obj</c> and <c>endobj</c>.
    /// </summary>
    public HandmadePdf Object(int number, string body, int generation = 0)
    {
        _offsets[number] = _file.Length;
        _file.Append(number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(generation.ToString(CultureInfo.InvariantCulture))
            .Append(" obj\n").Append(body).Append("\nendobj\n");
        return this;
    }

    /// <summary>Appends a stream object holding <paramref name="data"/>, its length as given or its true one.</summary>
    public HandmadePdf Stream(int number, string dictionary, string data, string? length = null)
    {
        string entries = dictionary.TrimEnd('>').TrimEnd('>') + $" /Length {length ?? data.Length.ToString(CultureInfo.InvariantCulture)}>>";
        return Object(number, $"{entries}\nstream\n{data}\nendstream");
    }

    /// <summary>Appends raw text anywhere in the file.</summary>
    public HandmadePdf Raw(string text)
    {
        _file.Append(text);
        return this;
    }

    /// <summary>
    /// Appends a cross-reference table for the objects written since the last one, and a trailer with
    /// <paramref name="trailer"/>'s entries, pointing back at the previous section; returns the table's offset.
    /// </summary>
    public int Section(string trailer, Func<int, int>? misplace = null, bool startAtOne = false)
    {
        int table = _file.Length;
        _file.Append("xref\n");

        List<int> numbers = _offsets.Keys.ToList();
        _file.Append(startAtOne ? "1" : "0").Append(' ').Append(numbers.Max() + 1).Append('\n');
        _file.Append("0000000000 65535 f\r\n");

        for (int number = 1; number <= numbers.Max(); number++)
        {
            if (_offsets.TryGetValue(number, out int offset))
                _file.Append((misplace?.Invoke(number) ?? offset).ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
            else
                _file.Append("0000000000 00000 f\r\n");
        }

        string previous = _lastSection >= 0 ? $" /Prev {_lastSection}" : string.Empty;
        _file.Append("trailer\n<<").Append(trailer).Append(" /Size ").Append(numbers.Max() + 1).Append(previous).Append(">>\n");
        _file.Append("startxref\n").Append(table).Append("\n%%EOF\n");
        _lastSection = table;
        return table;
    }

    /// <summary>A file of one page, US Letter unless the page says, with the given extra objects.</summary>
    public static HandmadePdf OnePage(string page = "") =>
        new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, $"<</Type/Page/Parent 2 0 R{page}>>");

    public byte[] ToArray() => Encoding.Latin1.GetBytes(_file.ToString());

    public override string ToString() => _file.ToString();
}
