using System.Globalization;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// Reads the objects of a PDF file from its bytes (ISO 32000-1, 7.2–7.3): numbers, names, strings, arrays,
/// dictionaries and references, and the keywords between them.
/// </summary>
/// <remarks>
/// Values are read into the writer's own types, so what is read can be written again without conversion: names keep
/// their bytes, and reals the text they were written as.
/// </remarks>
internal sealed class PdfParser(byte[] data, int position = 0)
{
    /// <summary>How deeply arrays and dictionaries may nest before the file is taken to be damaged.</summary>
    private const int MaximumDepth = 256;

    public byte[] Data { get; } = data;

    public int Position { get; set; } = position;

    public static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    public static bool IsDelimiter(byte value) =>
        value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    public static bool IsRegular(byte value) => !IsWhitespace(value) && !IsDelimiter(value);

    /// <summary>Skips whitespace and comments.</summary>
    public void SkipWhitespace()
    {
        while (Position < Data.Length)
        {
            byte value = Data[Position];

            if (IsWhitespace(value))
            {
                Position++;
            }
            else if (value == '%')
            {
                while (Position < Data.Length && Data[Position] is not (byte)'\n' and not (byte)'\r')
                    Position++;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>The next run of regular characters — a keyword or a number — after any whitespace.</summary>
    public ReadOnlySpan<byte> ReadToken()
    {
        SkipWhitespace();
        int start = Position;

        while (Position < Data.Length && IsRegular(Data[Position]))
            Position++;

        return Data.AsSpan(start, Position - start);
    }

    /// <summary>Reads <paramref name="keyword"/> if it comes next, leaving the position unchanged if not.</summary>
    public bool TryReadKeyword(ReadOnlySpan<byte> keyword)
    {
        int start = Position;

        if (ReadToken().SequenceEqual(keyword))
            return true;

        Position = start;
        return false;
    }

    /// <summary>Reads a non-negative integer if one comes next, leaving the position unchanged if not.</summary>
    public bool TryReadInteger(out long value)
    {
        int start = Position;
        ReadOnlySpan<byte> token = ReadToken();

        if (token.Length > 0 && token.Length <= 18 && IsDigits(token))
        {
            value = ParseDigits(token);
            return true;
        }

        Position = start;
        value = 0;
        return false;
    }

    public PdfValue ReadValue() => ReadValue(0);

    private PdfValue ReadValue(int depth)
    {
        if (depth > MaximumDepth)
            throw Damaged("arrays and dictionaries nest deeper than any real file");

        SkipWhitespace();

        if (Position >= Data.Length)
            throw Damaged("the file ends where an object was expected");

        switch (Data[Position])
        {
            case (byte)'/':
                return ReadName();

            case (byte)'(':
                return ReadLiteralString();

            case (byte)'<' when Position + 1 < Data.Length && Data[Position + 1] == '<':
                return ReadDictionary(depth);

            case (byte)'<':
                return ReadHexString();

            case (byte)'[':
                return ReadArray(depth);
        }

        int start = Position;
        ReadOnlySpan<byte> token = ReadToken();

        if (token.Length == 0)
            throw Damaged($"'{(char)Data[start]}' cannot begin an object");

        if (token.SequenceEqual("true"u8))
            return true;

        if (token.SequenceEqual("false"u8))
            return false;

        if (token.SequenceEqual("null"u8))
            return PdfValue.Null;

        if (IsDigits(token) && token.Length <= 10 && TryReadReferenceTail())
        {
            long number = ParseDigits(token);

            // Object 0 is always free, so a reference to it refers to nothing.
            return number is > 0 and <= int.MaxValue ? new PdfReference((int)number) : PdfValue.Null;
        }

        return Number(token, start);
    }

    /// <summary>
    /// After an integer, whether a generation and <c>R</c> follow, consuming them if so. The generation is not kept:
    /// the cross-reference section says which generation of an object is current.
    /// </summary>
    private bool TryReadReferenceTail()
    {
        int start = Position;

        if (TryReadInteger(out long value) && value <= int.MaxValue)
        {
            SkipWhitespace();

            if (Position < Data.Length && Data[Position] == 'R' && (Position + 1 == Data.Length || !IsRegular(Data[Position + 1])))
            {
                Position++;
                return true;
            }
        }

        Position = start;
        return false;
    }

    private PdfValue Number(ReadOnlySpan<byte> token, int start)
    {
        bool integer = true;
        int digits = 0;

        for (int index = 0; index < token.Length; index++)
        {
            byte value = token[index];

            if (value is >= (byte)'0' and <= (byte)'9')
                digits++;
            else if (value == '.' && integer)
                integer = false;
            else if (!(index == 0 && value is (byte)'+' or (byte)'-'))
                throw Damaged($"'{Text(token)}' at {start} is neither a number nor a keyword an object can be");
        }

        if (digits == 0)
            throw Damaged($"'{Text(token)}' at {start} is not a number");

        string text = Text(token);

        if (integer && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
            return whole;

        double real = double.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return PdfValue.ReadReal(real, token.ToArray());
    }

    private PdfName ReadName()
    {
        Position++;
        int start = Position;

        while (Position < Data.Length && IsRegular(Data[Position]))
            Position++;

        ReadOnlySpan<byte> raw = Data.AsSpan(start, Position - start);

        if (raw.IndexOf((byte)'#') < 0)
            return PdfName.FromBytes(raw);

        List<byte> bytes = new List<byte>(raw.Length);

        for (int index = 0; index < raw.Length; index++)
        {
            if (raw[index] == '#' && index + 2 < raw.Length && HexValue(raw[index + 1]) is int high and >= 0 && HexValue(raw[index + 2]) is int low and >= 0)
            {
                bytes.Add((byte)((high << 4) | low));
                index += 2;
            }
            else
            {
                bytes.Add(raw[index]);
            }
        }

        // A name escaping the null byte is damaged; the name up to it is what a reader can use.
        int end = bytes.IndexOf(0);
        return PdfName.FromBytes(bytes.Take(end < 0 ? bytes.Count : end).ToArray());
    }

    private PdfString ReadLiteralString()
    {
        Position++;
        List<byte> bytes = [];
        int depth = 1;

        while (Position < Data.Length)
        {
            byte value = Data[Position++];

            switch (value)
            {
                case (byte)'(':
                    depth++;
                    bytes.Add(value);
                    break;

                case (byte)')':
                    if (--depth == 0)
                        return new PdfString(bytes.ToArray());

                    bytes.Add(value);
                    break;

                case (byte)'\\':
                    Escape(bytes);
                    break;

                case (byte)'\r':
                    // An end of line inside a string is a line feed, however it was written.
                    if (Position < Data.Length && Data[Position] == '\n')
                        Position++;

                    bytes.Add((byte)'\n');
                    break;

                default:
                    bytes.Add(value);
                    break;
            }
        }

        throw Damaged("a string runs to the end of the file");
    }

    private void Escape(List<byte> bytes)
    {
        if (Position >= Data.Length)
            return;

        byte value = Data[Position++];

        switch (value)
        {
            case (byte)'n':
                bytes.Add((byte)'\n');
                break;
            case (byte)'r':
                bytes.Add((byte)'\r');
                break;
            case (byte)'t':
                bytes.Add((byte)'\t');
                break;
            case (byte)'b':
                bytes.Add(8);
                break;
            case (byte)'f':
                bytes.Add(12);
                break;
            case (byte)'\r':
                // A backslash before an end of line joins the lines.
                if (Position < Data.Length && Data[Position] == '\n')
                    Position++;

                break;
            case (byte)'\n':
                break;
            case >= (byte)'0' and <= (byte)'7':
                int code = value - '0';

                for (int digit = 1; digit < 3 && Position < Data.Length && Data[Position] is >= (byte)'0' and <= (byte)'7'; digit++)
                    code = (code * 8) + (Data[Position++] - '0');

                bytes.Add((byte)code);
                break;
            default:
                // Parentheses, the backslash itself, and any other character a producer escaped for no reason.
                bytes.Add(value);
                break;
        }
    }

    private PdfString ReadHexString()
    {
        Position++;
        List<byte> bytes = [];
        int high = -1;

        while (Position < Data.Length)
        {
            byte value = Data[Position++];

            if (value == '>')
            {
                // An odd digit out is followed by a zero.
                if (high >= 0)
                    bytes.Add((byte)(high << 4));

                return new PdfString(bytes.ToArray(), PdfStringForm.Hex);
            }

            int digit = HexValue(value);

            if (digit < 0)
            {
                if (IsWhitespace(value))
                    continue;

                throw Damaged($"'{(char)value}' in a hexadecimal string");
            }

            if (high < 0)
            {
                high = digit;
            }
            else
            {
                bytes.Add((byte)((high << 4) | digit));
                high = -1;
            }
        }

        throw Damaged("a hexadecimal string runs to the end of the file");
    }

    private PdfArray ReadArray(int depth)
    {
        Position++;
        PdfArray array = new PdfArray();

        while (true)
        {
            SkipWhitespace();

            if (Position >= Data.Length)
                throw Damaged("an array runs to the end of the file");

            if (Data[Position] == ']')
            {
                Position++;
                return array;
            }

            array.Add(ReadValue(depth + 1));
        }
    }

    private PdfDictionary ReadDictionary(int depth)
    {
        Position += 2;
        PdfDictionary dictionary = new PdfDictionary();

        while (true)
        {
            SkipWhitespace();

            if (Position >= Data.Length)
                throw Damaged("a dictionary runs to the end of the file");

            if (Data[Position] == '>')
            {
                if (Position + 1 < Data.Length && Data[Position + 1] == '>')
                {
                    Position += 2;
                    return dictionary;
                }

                throw Damaged("a dictionary ends with a single '>'");
            }

            if (Data[Position] != '/')
                throw Damaged($"a dictionary key at {Position} is not a name");

            PdfName key = ReadName();
            PdfValue value = ReadValue(depth + 1);

            // A null value is the same as no entry at all (7.3.7).
            if (value.Kind != PdfValueKind.Null)
                dictionary[key] = value;
        }
    }

    public UnreadableFileException Damaged(string what) =>
        new UnreadableFileException($"The file is not a PDF this library can read: {what}.");

    private static bool IsDigits(ReadOnlySpan<byte> token)
    {
        foreach (byte value in token)
        {
            if (value is < (byte)'0' or > (byte)'9')
                return false;
        }

        return true;
    }

    private static long ParseDigits(ReadOnlySpan<byte> token)
    {
        long value = 0;

        foreach (byte digit in token)
            value = (value * 10) + (digit - '0');

        return value;
    }

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        _ => -1,
    };

    private static string Text(ReadOnlySpan<byte> token)
    {
        char[] characters = new char[token.Length];

        for (int index = 0; index < token.Length; index++)
            characters[index] = (char)token[index];

        return new string(characters);
    }
}
