using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// Reads a complete file back through its cross-reference section — table or stream — resolving objects at their
/// recorded offsets and inside object streams. Every lookup checks that the offset really points at
/// <c>N 0 obj</c>, so a wrong offset fails the test that follows it rather than silently reading a neighbour.
/// </summary>
internal sealed class PdfFileReader
{
    private readonly byte[] _file;
    private readonly Dictionary<int, (int Type, long Field2, int Field3)> _entries = new Dictionary<int, (int, long, int)>();

    public PdfFileReader(byte[] file)
    {
        _file = file;

        string tail = Encoding.ASCII.GetString(file, Math.Max(0, file.Length - 40), Math.Min(40, file.Length));
        if (!tail.EndsWith("\n%%EOF\n", StringComparison.Ordinal))
            throw new FormatException("The file does not end with %%EOF.");

        int marker = tail.LastIndexOf("startxref\n", StringComparison.Ordinal);
        string offset = tail.Substring(marker + 10, tail.Length - marker - 10 - 7);
        StartXref = long.Parse(offset, CultureInfo.InvariantCulture);

        if (Encoding.ASCII.GetString(file, (int)StartXref, 4) == "xref")
            Trailer = ReadTable();
        else
            Trailer = ReadStream();
    }

    public long StartXref { get; }

    public bool HasCrossReferenceStream { get; private set; }

    public Dictionary<string, object?> Trailer { get; }

    public IReadOnlyDictionary<int, (int Type, long Field2, int Field3)> Entries => _entries;

    public int Size => checked((int)(long)Trailer["Size"]!);

    public object? GetObject(int number)
    {
        (int type, long field2, int field3) = _entries[number];
        if (type == 1)
        {
            PdfSyntaxParser parser = new PdfSyntaxParser(_file, checked((int)field2));
            string header = $"{number} 0 obj";
            if (Encoding.ASCII.GetString(_file, (int)field2, header.Length) != header)
                throw new FormatException($"The offset {field2} of object {number} does not point at '{header}'.");

            parser.Expect(number.ToString(CultureInfo.InvariantCulture));
            parser.Expect("0");
            parser.Expect("obj");
            object? value = parser.ReadObject();
            parser.Expect("endobj");
            return value;
        }

        if (type == 2)
        {
            ParsedStream container = (ParsedStream)GetObject(checked((int)field2))!;
            if (!Equals(container.Dictionary["Type"], new ParsedName("ObjStm")))
                throw new FormatException($"Object {field2} is not an object stream.");

            byte[] content = Decode(container);
            PdfSyntaxParser parser = new PdfSyntaxParser(content);
            long count = (long)container.Dictionary["N"]!;
            long first = (long)container.Dictionary["First"]!;
            for (int index = 0; index < count; index++)
            {
                long containedNumber = (long)parser.ReadObject()!;
                long containedOffset = (long)parser.ReadObject()!;
                if (index == field3)
                {
                    if (containedNumber != number)
                        throw new FormatException($"Index {field3} of object stream {field2} holds object {containedNumber}, not {number}.");

                    return new PdfSyntaxParser(content, checked((int)(first + containedOffset))).ReadObject();
                }
            }

            throw new FormatException($"Object stream {field2} has no index {field3}.");
        }

        throw new FormatException($"Object {number} is free.");
    }

    public object? Resolve(object? value) => value is ParsedReference reference ? GetObject(reference.ObjectNumber) : value;

    public Dictionary<string, object?> Dictionary(object? value) =>
        Resolve(value) switch
        {
            Dictionary<string, object?> dictionary => dictionary,
            ParsedStream stream => stream.Dictionary,
            object other => throw new FormatException($"Expected a dictionary but found {other}."),
            null => throw new FormatException("Expected a dictionary but found null."),
        };

    /// <summary>The catalog, found through the trailer's <c>/Root</c>.</summary>
    public Dictionary<string, object?> Catalog() => Dictionary(Trailer["Root"]);

    /// <summary>
    /// Walks the page tree from the catalog, checking at every node that <c>/Parent</c> points back up and that
    /// <c>/Count</c> matches the pages beneath. Returns each page's object number and depth, in document order.
    /// </summary>
    public List<(int Number, int Depth)> Pages()
    {
        List<(int Number, int Depth)> pages = new List<(int Number, int Depth)>();
        WalkPages((ParsedReference)Catalog()["Pages"]!, null, 0, pages);
        return pages;
    }

    /// <summary>
    /// Walks a name tree, checking that the root has no <c>/Limits</c> and that every other node's limits are the
    /// first and last keys beneath it. Returns each entry with the depth of its leaf, in tree order.
    /// </summary>
    public List<(byte[] Key, object? Value, int Depth)> NameTree(object? root)
    {
        List<(byte[] Key, object? Value, int Depth)> entries = new List<(byte[] Key, object? Value, int Depth)>();
        Dictionary<string, object?> node = Dictionary(root);
        if (node.ContainsKey("Limits"))
            throw new FormatException("The root of a name tree must not have /Limits.");

        WalkNames(node, 0, entries);
        return entries;
    }

    /// <summary>The stream's data with its filter and predictor undone.</summary>
    public static byte[] Decode(ParsedStream stream)
    {
        if (!stream.Dictionary.TryGetValue("Filter", out object? filter))
            return stream.Data;

        if (!Equals(filter, new ParsedName("FlateDecode")))
            throw new FormatException($"Unsupported filter {filter}.");

        byte[] inflated = ZlibReader.Inflate(stream.Data);
        if (!stream.Dictionary.TryGetValue("DecodeParms", out object? parameters))
            return inflated;

        Dictionary<string, object?> decode = (Dictionary<string, object?>)parameters!;
        if ((long)decode["Predictor"]! < 10)
            throw new FormatException("Only PNG predictors are supported.");

        return Unpredict(inflated, checked((int)(long)decode["Columns"]!));
    }

    private int WalkPages(ParsedReference reference, ParsedReference? parent, int depth, List<(int Number, int Depth)> pages)
    {
        Dictionary<string, object?> node = Dictionary(reference);
        if (parent == null ? node.ContainsKey("Parent") : !Equals(node["Parent"], parent))
            throw new FormatException($"Object {reference.ObjectNumber} does not name {parent?.ToString() ?? "no parent"} as its parent.");

        string type = ((ParsedName)node["Type"]!).Value;
        if (type == "Page")
        {
            pages.Add((reference.ObjectNumber, depth));
            return 1;
        }

        if (type != "Pages")
            throw new FormatException($"Object {reference.ObjectNumber} is a /{type}, not a page tree node.");

        int count = 0;
        foreach (object? kid in (List<object?>)node["Kids"]!)
            count += WalkPages((ParsedReference)kid!, reference, depth + 1, pages);

        if ((long)node["Count"]! != count)
            throw new FormatException($"Page tree node {reference.ObjectNumber} has /Count {node["Count"]} but {count} pages beneath it.");

        return count;
    }

    private (byte[] First, byte[] Last) WalkNames(Dictionary<string, object?> node, int depth, List<(byte[] Key, object? Value, int Depth)> entries)
    {
        int start = entries.Count;
        if (node.TryGetValue("Names", out object? names))
        {
            List<object?> pairs = (List<object?>)names!;
            for (int index = 0; index < pairs.Count; index += 2)
                entries.Add(((byte[])pairs[index]!, pairs[index + 1], depth));
        }
        else
        {
            foreach (object? kid in (List<object?>)node["Kids"]!)
            {
                Dictionary<string, object?> child = Dictionary(kid);
                (byte[] first, byte[] last) = WalkNames(child, depth + 1, entries);
                List<object?> limits = (List<object?>)child["Limits"]!;
                if (!((byte[])limits[0]!).SequenceEqual(first) || !((byte[])limits[1]!).SequenceEqual(last))
                    throw new FormatException("A name tree node's /Limits are not the first and last keys beneath it.");
            }
        }

        return (entries[start].Key, entries[entries.Count - 1].Key);
    }

    private static byte[] Unpredict(byte[] rows, int columns)
    {
        if (rows.Length % (columns + 1) != 0)
            throw new FormatException("Predicted data is not a whole number of rows.");

        int count = rows.Length / (columns + 1);
        byte[] output = new byte[count * columns];
        for (int row = 0; row < count; row++)
        {
            byte tag = rows[row * (columns + 1)];
            for (int column = 0; column < columns; column++)
            {
                byte raw = rows[(row * (columns + 1)) + 1 + column];
                byte above = row == 0 ? (byte)0 : output[((row - 1) * columns) + column];
                output[(row * columns) + column] = tag switch
                {
                    0 => raw,
                    2 => (byte)(raw + above),
                    _ => throw new FormatException($"Unsupported PNG row filter {tag}."),
                };
            }
        }

        return output;
    }

    private Dictionary<string, object?> ReadTable()
    {
        PdfSyntaxParser parser = new PdfSyntaxParser(_file, (int)StartXref);
        parser.Expect("xref");
        int first = int.Parse(parser.ReadKeyword(), CultureInfo.InvariantCulture);
        int count = int.Parse(parser.ReadKeyword(), CultureInfo.InvariantCulture);
        if (_file[parser.Position] != '\n')
            throw new FormatException("The subsection header must end with a line feed.");

        int position = parser.Position + 1;
        for (int number = first; number < first + count; number++)
        {
            string line = Encoding.ASCII.GetString(_file, position, 20);
            if (line[10] != ' ' || line[16] != ' ' || (line.Substring(18) != " \n" && line.Substring(18) != "\r\n"))
                throw new FormatException($"Entry {number} is not a twenty-byte line: '{line}'.");

            long offset = long.Parse(line.Substring(0, 10), CultureInfo.InvariantCulture);
            int generation = int.Parse(line.Substring(11, 5), CultureInfo.InvariantCulture);
            _entries[number] = line[17] switch
            {
                'n' => (1, offset, generation),
                'f' => (0, offset, generation),
                _ => throw new FormatException($"Entry {number} has type '{line[17]}'."),
            };

            position += 20;
        }

        parser.Position = position;
        parser.Expect("trailer");
        return (Dictionary<string, object?>)parser.ReadObject()!;
    }

    private Dictionary<string, object?> ReadStream()
    {
        HasCrossReferenceStream = true;
        PdfSyntaxParser parser = new PdfSyntaxParser(_file, (int)StartXref);
        int number = int.Parse(parser.ReadKeyword(), CultureInfo.InvariantCulture);
        parser.Expect("0");
        parser.Expect("obj");
        ParsedStream stream = (ParsedStream)parser.ReadObject()!;
        parser.Expect("endobj");

        if (!Equals(stream.Dictionary["Type"], new ParsedName("XRef")))
            throw new FormatException("The object at startxref is not a cross-reference stream.");

        List<object?> widths = (List<object?>)stream.Dictionary["W"]!;
        int[] w = widths.Select(width => checked((int)(long)width!)).ToArray();
        byte[] rows = Decode(stream);
        int rowLength = w.Sum();
        int size = checked((int)(long)stream.Dictionary["Size"]!);
        if (rows.Length != size * rowLength)
            throw new FormatException($"{rows.Length} bytes of rows for /Size {size} and /W [{string.Join(" ", w)}].");

        for (int index = 0; index < size; index++)
        {
            int position = index * rowLength;
            long[] fields = new long[3];
            for (int field = 0; field < 3; field++)
            {
                for (int column = 0; column < w[field]; column++)
                    fields[field] = (fields[field] << 8) | rows[position++];
            }

            _entries[index] = (checked((int)fields[0]), fields[1], checked((int)fields[2]));
        }

        if (_entries[number] != (1, StartXref, 0))
            throw new FormatException("The cross-reference stream does not list itself at its own offset.");

        return stream.Dictionary;
    }
}
