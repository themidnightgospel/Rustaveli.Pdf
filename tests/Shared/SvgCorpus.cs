using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Rustaveli.Pdf.Testing;

/// <summary>
/// The SVG documents of resvg's test suite at assets/svg/resvg beside the test assembly: every element and property of
/// SVG drawn in small, odd and broken ways (assets/svg/resvg/README.md says where they come from).
/// </summary>
public static class SvgCorpus
{
    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "assets", "svg", "resvg");

    /// <summary>The documents the reader may refuse, as it documents, with a <see cref="FormatException"/>.</summary>
    public static IReadOnlyCollection<string> Refusable { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // No size to draw at.
        "structure/svg/negative-size.svg",
        "structure/svg/zero-size.svg",
    };

    /// <summary>Every document, by its path under the corpus with forward slashes, which is how a test names it.</summary>
    public static TheoryData<string> Documents
    {
        get
        {
            TheoryData<string> documents = new TheoryData<string>();

            foreach (string file in Directory.EnumerateFiles(Folder, "*.svg", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.Ordinal))
                documents.Add(file.Substring(Folder.Length + 1).Replace('\\', '/'));

            return documents;
        }
    }

    public static string PathOf(string document) => Path.Combine(Folder, document.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// The size, in points, a document gives itself where it says so plainly: a width and height in pixels, or a view
    /// box to take them from. Null where it says so in units, percentages or values that are no size.
    /// </summary>
    public static Extent? PlainSize(string document)
    {
        XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        using XmlReader reader = XmlReader.Create(PathOf(document), settings);
        XElement root = XDocument.Load(reader).Root!;
        float[]? viewBox = ((string?)root.Attribute("viewBox"))?.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number : float.NaN)
            .ToArray();

        if (viewBox is not null && (viewBox.Length != 4 || viewBox.Any(float.IsNaN) || !(viewBox[2] > 0) || !(viewBox[3] > 0)))
            return null;

        float? width = Pixels((string?)root.Attribute("width"), viewBox?[2] ?? 300);
        float? height = Pixels((string?)root.Attribute("height"), viewBox?[3] ?? 150);

        return width > 0 && height > 0 ? new Extent(width.Value * 0.75f, height.Value * 0.75f) : null;
    }

    private static float? Pixels(string? text, float absent)
    {
        if (text is null)
            return absent;

        string number = text.Trim();

        if (number.EndsWith("px", StringComparison.Ordinal))
            number = number.Substring(0, number.Length - 2);

        return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float pixels) ? pixels : null;
    }
}
