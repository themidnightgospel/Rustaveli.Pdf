using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// A deliberately small, independent reader of PDF object syntax, so tests can check what the writer produced by
/// parsing it back rather than by trusting the writer's own model.
/// </summary>
/// <remarks>
/// Values come back as plain .NET objects: <c>null</c>, <see cref="bool"/>, <see cref="long"/> for integers,
/// <see cref="double"/> for reals, <see cref="ParsedName"/>, <see cref="byte"/> arrays for strings,
/// <see cref="List{T}"/> for arrays, <see cref="Dictionary{TKey,TValue}"/> keyed by name for dictionaries,
/// <see cref="ParsedReference"/> and <see cref="ParsedStream"/>. It follows ISO 32000-1, 7.2–7.3, and throws on
/// anything it does not understand rather than guessing.
/// </remarks>
internal sealed class PdfSyntaxParser(byte[] data, int position = 0)
{
    public int Position { get; set; } = position;

    /// <summary>Parses <paramref name="text"/> as exactly one object, failing if anything else follows.</summary>
    public static object? ParseSingle(byte[] text)
    {
        PdfSyntaxParser parser = new PdfSyntaxParser(text);
        object? value = parser.ReadObject();
        parser.SkipWhitespace();
        if (parser.Position != text.Length)
            throw new FormatException($"Unexpected content at {parser.Position} after the object.");

        return value;
    }

    public object? ReadObject()
    {
        SkipWhitespace();
        byte next = Peek();
        switch (next)
        {
            case (byte)'/':
                return ReadName();
            case (byte)'(':
                return ReadLiteralString();
            case (byte)'[':
                return ReadArray();
            case (byte)'<' when PeekAt(1) == '<':
                return ReadDictionaryOrStream();
            case (byte)'<':
                return ReadHexString();
        }

        if (next is (byte)'+' or (byte)'-' or (byte)'.' || IsDigit(next))
            return ReadNumberOrReference();

        string keyword = ReadKeyword();
        return keyword switch
        {
            "null" => null,
            "true" => true,
            "false" => false,
            _ => throw new FormatException($"Unexpected keyword '{keyword}' at {Position - keyword.Length}."),
        };
    }

    /// <summary>Reads a run of regular characters: a keyword, an operator or a number's text.</summary>
    public string ReadKeyword()
    {
        SkipWhitespace();
        int start = Position;
        while (Position < data.Length && IsRegular(data[Position]))
            Position++;

        if (Position == start)
            throw new FormatException($"Expected a token at {start}.");

        return Encoding.ASCII.GetString(data, start, Position - start);
    }

    public void Expect(string keyword)
    {
        string actual = ReadKeyword();
        if (actual != keyword)
            throw new FormatException($"Expected '{keyword}' but read '{actual}'.");
    }

    public void SkipWhitespace()
    {
        while (Position < data.Length)
        {
            byte current = data[Position];
            if (current == '%')
            {
                while (Position < data.Length && data[Position] != '\n' && data[Position] != '\r')
                    Position++;
            }
            else if (IsWhitespace(current))
            {
                Position++;
            }
            else
            {
                return;
            }
        }
    }

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte value) => "()<>[]{}/%".IndexOf((char)value) >= 0;

    private static bool IsRegular(byte value) => !IsWhitespace(value) && !IsDelimiter(value);

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        _ => throw new FormatException($"'{(char)value}' is not a hexadecimal digit."),
    };

    private byte Peek() => Position < data.Length ? data[Position] : throw new FormatException("Unexpected end of data.");

    private int PeekAt(int offset) => Position + offset < data.Length ? data[Position + offset] : -1;

    private ParsedName ReadName()
    {
        Position++;
        List<byte> bytes = new List<byte>();
        while (Position < data.Length && IsRegular(data[Position]))
        {
            if (data[Position] == '#')
            {
                bytes.Add((byte)((HexValue(data[Position + 1]) << 4) | HexValue(data[Position + 2])));
                Position += 3;
            }
            else
            {
                bytes.Add(data[Position++]);
            }
        }

        return new ParsedName(Encoding.UTF8.GetString(bytes.ToArray()));
    }

    private byte[] ReadLiteralString()
    {
        Position++;
        List<byte> bytes = new List<byte>();
        int depth = 1;
        while (true)
        {
            byte current = data[Position++];
            switch (current)
            {
                case (byte)'(':
                    depth++;
                    bytes.Add(current);
                    break;
                case (byte)')':
                    if (--depth == 0)
                        return bytes.ToArray();

                    bytes.Add(current);
                    break;
                case (byte)'\\':
                    ReadEscape(bytes);
                    break;
                case (byte)'\r':
                    // An unescaped end-of-line inside a string reads as a single line feed, whatever its form.
                    if (PeekAt(0) == '\n')
                        Position++;

                    bytes.Add((byte)'\n');
                    break;
                default:
                    bytes.Add(current);
                    break;
            }
        }
    }

    private void ReadEscape(List<byte> bytes)
    {
        byte escaped = data[Position++];
        switch (escaped)
        {
            case (byte)'n': bytes.Add((byte)'\n'); break;
            case (byte)'r': bytes.Add((byte)'\r'); break;
            case (byte)'t': bytes.Add((byte)'\t'); break;
            case (byte)'b': bytes.Add((byte)'\b'); break;
            case (byte)'f': bytes.Add((byte)'\f'); break;
            case (byte)'\r':
                if (PeekAt(0) == '\n')
                    Position++;

                break;
            case (byte)'\n':
                break;
            case >= (byte)'0' and <= (byte)'7':
                int code = escaped - '0';
                for (int digits = 1; digits < 3 && PeekAt(0) is >= '0' and <= '7'; digits++)
                    code = (code * 8) + (data[Position++] - '0');

                bytes.Add((byte)code);
                break;
            default:
                bytes.Add(escaped);
                break;
        }
    }

    private byte[] ReadHexString()
    {
        Position++;
        List<int> digits = new List<int>();
        while (data[Position] != '>')
        {
            if (!IsWhitespace(data[Position]))
                digits.Add(HexValue(data[Position]));

            Position++;
        }

        Position++;
        if (digits.Count % 2 == 1)
            digits.Add(0);

        byte[] bytes = new byte[digits.Count / 2];
        for (int index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)((digits[2 * index] << 4) | digits[(2 * index) + 1]);

        return bytes;
    }

    private List<object?> ReadArray()
    {
        Position++;
        List<object?> items = new List<object?>();
        while (true)
        {
            SkipWhitespace();
            if (Peek() == ']')
            {
                Position++;
                return items;
            }

            items.Add(ReadObject());
        }
    }

    private object ReadDictionaryOrStream()
    {
        Position += 2;
        Dictionary<string, object?> dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        while (true)
        {
            SkipWhitespace();
            if (Peek() == '>')
            {
                if (PeekAt(1) != '>')
                    throw new FormatException($"Expected '>>' at {Position}.");

                Position += 2;
                break;
            }

            if (Peek() != '/')
                throw new FormatException($"Expected a name as dictionary key at {Position}.");

            string key = ReadName().Value;
            if (dictionary.ContainsKey(key))
                throw new FormatException($"Duplicate dictionary key /{key}.");

            dictionary[key] = ReadObject();
        }

        int afterDictionary = Position;
        SkipWhitespace();
        if (Position + 6 > data.Length || Encoding.ASCII.GetString(data, Position, 6) != "stream")
        {
            Position = afterDictionary;
            return dictionary;
        }

        Position += 6;
        if (data[Position] == '\r')
            Position++;

        if (data[Position++] != '\n')
            throw new FormatException("The stream keyword must be followed by an end-of-line.");

        int length = checked((int)(long)dictionary["Length"]!);
        byte[] streamData = data.AsSpan(Position, length).ToArray();
        Position += length;
        Expect("endstream");
        return new ParsedStream(dictionary, streamData);
    }

    private object ReadNumberOrReference()
    {
        string token = ReadKeyword();
        if (token.IndexOf('.') >= 0)
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);

        long number = long.Parse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        // "12 0 R" is a reference; anything else leaves the integer standing on its own.
        int afterNumber = Position;
        SkipWhitespace();
        if (Position < data.Length && IsDigit(data[Position]))
        {
            string generation = ReadKeyword();
            SkipWhitespace();
            if (generation.All(character => character is >= '0' and <= '9')
                && Position < data.Length && data[Position] == 'R'
                && (Position + 1 == data.Length || !IsRegular(data[Position + 1])))
            {
                Position++;
                return new ParsedReference(checked((int)number), int.Parse(generation, CultureInfo.InvariantCulture));
            }
        }

        Position = afterNumber;
        return number;
    }
}
