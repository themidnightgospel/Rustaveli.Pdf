using System.Buffers;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Accumulates PDF syntax in a growable byte buffer rented from the shared array pool. Every serialiser in the
/// writer — objects, object streams, content streams — writes through one of these, so each token has exactly one
/// implementation.
/// </summary>
/// <remarks>
/// Output is compact: a space is inserted between two tokens only when both touching bytes are regular characters,
/// which is the only case where a reader would otherwise run them together. <c>/Type/Page</c>, <c>[1 0 R]</c> and
/// <c>(text)Tj</c> need none. Dispose returns the buffer to the pool.
/// </remarks>
internal sealed class PdfByteWriter : IDisposable
{
    /// <summary>
    /// How deeply arrays and dictionaries may nest. Nothing a document needs comes close; an object graph that
    /// reaches it almost certainly contains itself, and failing here beats overflowing the stack.
    /// </summary>
    public const int MaxNestingDepth = 64;

    private byte[] _buffer;
    private int _length;

    public PdfByteWriter(int initialCapacity = 256)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    }

    public int Length => _length;

    public ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _length);

    /// <summary>The written bytes as an array segment, for APIs that predate spans.</summary>
    public ArraySegment<byte> WrittenSegment => new ArraySegment<byte>(_buffer, 0, _length);

    /// <summary>
    /// The shorter of the literal and hex forms of <paramref name="bytes"/>, preferring literal on a tie.
    /// </summary>
    public static PdfStringForm CompactForm(ReadOnlySpan<byte> bytes) =>
        LiteralLength(bytes) <= 2 + (2 * bytes.Length) ? PdfStringForm.Literal : PdfStringForm.Hex;

    /// <summary>
    /// The length of <paramref name="bytes"/> written as a literal string, parentheses and escapes included.
    /// </summary>
    public static int LiteralLength(ReadOnlySpan<byte> bytes)
    {
        int length = 2;
        foreach (byte value in bytes)
            length += EscapeLength(value);

        return length;
    }

    public void Clear() => _length = 0;

    /// <summary>
    /// A span of at least <paramref name="sizeHint"/> bytes at the end of the buffer; see <see cref="Advance"/>.
    /// </summary>
    public Span<byte> GetSpan(int sizeHint)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_length);
    }

    /// <summary>Commits <paramref name="count"/> bytes written into the span <see cref="GetSpan"/> returned.</summary>
    public void Advance(int count) => _length += count;

    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_length++] = value;
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
    }

    /// <summary>Writes a keyword or operator such as <c>null</c>, <c>obj</c> or <c>re</c>.</summary>
    public void WriteKeyword(ReadOnlySpan<byte> keyword)
    {
        Separate();
        Write(keyword);
    }

    public void WriteInteger(long value)
    {
        EnsureCapacity(PdfNumbers.MaxLength + 1);
        int separator = SeparatorLength();
        int written = PdfNumbers.WriteInteger(value, _buffer.AsSpan(_length + separator));
        CommitNumber(separator, written);
    }

    public void WriteReal(double value)
    {
        // Formatted past the separator first: an invalid value throws before the buffer has changed at all.
        EnsureCapacity(PdfNumbers.MaxLength + 1);
        int separator = SeparatorLength();
        int written = PdfNumbers.WriteReal(value, _buffer.AsSpan(_length + separator));
        CommitNumber(separator, written);
    }

    public void WriteName(PdfName name) => Write(name.Encoded);

    public void WriteReference(PdfReference reference)
    {
        WriteInteger(reference.ObjectNumber);
        Write(" 0 R"u8);
    }

    public void WriteString(PdfString value)
    {
        if (value.Form == PdfStringForm.Hex)
            WriteHexString(value.Bytes);
        else
            WriteLiteralString(value.Bytes);
    }

    /// <summary>
    /// Writes <c>(…)</c>. Parentheses and backslashes are escaped, as are control characters: CR and LF would
    /// otherwise be normalised by readers, and the rest would make the file fragile to anything that treats it as text.
    /// </summary>
    public void WriteLiteralString(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(2 + (4 * bytes.Length));
        Span<byte> target = _buffer.AsSpan(_length);
        int position = 0;

        target[position++] = (byte)'(';
        foreach (byte value in bytes)
        {
            byte escape = value switch
            {
                (byte)'(' or (byte)')' or (byte)'\\' => value,
                (byte)'\n' => (byte)'n',
                (byte)'\r' => (byte)'r',
                (byte)'\t' => (byte)'t',
                (byte)'\b' => (byte)'b',
                (byte)'\f' => (byte)'f',
                _ => 0,
            };

            if (escape != 0)
            {
                target[position++] = (byte)'\\';
                target[position++] = escape;
            }
            else if (value is < 0x20 or 0x7F)
            {
                target[position++] = (byte)'\\';
                target[position++] = (byte)('0' + (value >> 6));
                target[position++] = (byte)('0' + ((value >> 3) & 7));
                target[position++] = (byte)('0' + (value & 7));
            }
            else
            {
                target[position++] = value;
            }
        }

        target[position++] = (byte)')';
        _length += position;
    }

    public void WriteHexString(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(2 + (2 * bytes.Length));
        Span<byte> target = _buffer.AsSpan(_length);
        int position = 0;

        target[position++] = (byte)'<';
        foreach (byte value in bytes)
        {
            target[position++] = PdfCharacters.HexDigit(value >> 4);
            target[position++] = PdfCharacters.HexDigit(value);
        }

        target[position++] = (byte)'>';
        _length += position;
    }

    public void WriteValue(PdfValue value) => WriteValue(value, 0);

    public void WriteArray(PdfArray array) => WriteArray(array, 0);

    public void WriteDictionary(PdfDictionary dictionary) => WriteDictionary(dictionary, 0);

    /// <summary>
    /// Writes a dictionary's entries without the enclosing <c>&lt;&lt; &gt;&gt;</c>, for callers that append entries
    /// of their own — a stream's <c>/Length</c>, say.
    /// </summary>
    public void WriteDictionaryEntries(PdfDictionary dictionary) => WriteEntries(dictionary, 1);

    public void Dispose()
    {
        byte[] buffer = _buffer;
        _buffer = [];
        _length = 0;
        Release(buffer);
    }

    private static int EscapeLength(byte value) => value switch
    {
        (byte)'(' or (byte)')' or (byte)'\\' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\b' or (byte)'\f' => 2,
        < 0x20 or 0x7F => 4,
        _ => 1,
    };

    // An empty array was never rented; the pool would reject it.
    private static void Release(byte[] buffer)
    {
        if (buffer.Length > 0)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    private void WriteValue(PdfValue value, int depth)
    {
        switch (value.Kind)
        {
            case PdfValueKind.Null:
                WriteKeyword("null"u8);
                break;
            case PdfValueKind.Boolean:
                WriteKeyword(value.AsBoolean() ? "true"u8 : "false"u8);
                break;
            case PdfValueKind.Integer:
                WriteInteger(value.AsInteger());
                break;
            case PdfValueKind.Real when value.RealText is { } text:
                WriteKeyword(text);
                break;
            case PdfValueKind.Real:
                WriteReal(value.AsReal());
                break;
            case PdfValueKind.Name:
                WriteName(value.AsName());
                break;
            case PdfValueKind.String:
                WriteString(value.AsString());
                break;
            case PdfValueKind.Array:
                WriteArray(value.AsArray(), depth);
                break;
            case PdfValueKind.Dictionary:
                WriteDictionary(value.AsDictionary(), depth);
                break;
            default:
                WriteReference(value.AsReference());
                break;
        }
    }

    private void WriteArray(PdfArray array, int depth)
    {
        CheckDepth(depth);
        WriteByte((byte)'[');
        foreach (PdfValue item in array)
            WriteValue(item, depth + 1);

        WriteByte((byte)']');
    }

    private void WriteDictionary(PdfDictionary dictionary, int depth)
    {
        CheckDepth(depth);
        Write("<<"u8);
        WriteEntries(dictionary, depth + 1);
        Write(">>"u8);
    }

    private void WriteEntries(PdfDictionary dictionary, int depth)
    {
        foreach (KeyValuePair<PdfName, PdfValue> entry in dictionary)
        {
            WriteName(entry.Key);
            WriteValue(entry.Value, depth);
        }
    }

    private static void CheckDepth(int depth)
    {
        if (depth >= MaxNestingDepth)
        {
            throw new InvalidOperationException(
                $"Arrays and dictionaries nest more than {MaxNestingDepth} deep; "
                + "the object graph probably contains itself.");
        }
    }

    // A trailing solidus is the empty name, written as "/" alone: a delimiter, yet it would absorb a following
    // regular token into the name just as a regular character would.
    private int SeparatorLength()
    {
        if (_length == 0)
            return 0;

        byte last = _buffer[_length - 1];
        return PdfCharacters.IsRegular(last) || last == (byte)'/' ? 1 : 0;
    }

    private void Separate()
    {
        if (SeparatorLength() == 1)
            WriteByte((byte)' ');
    }

    private void CommitNumber(int separator, int written)
    {
        if (separator == 1)
            _buffer[_length] = (byte)' ';

        _length += separator + written;
    }

    private void EnsureCapacity(int additional)
    {
        int required = _length + additional;
        if (required <= _buffer.Length)
            return;

        byte[] larger = ArrayPool<byte>.Shared.Rent(Math.Max(required, 2 * _buffer.Length));
        _buffer.AsSpan(0, _length).CopyTo(larger);
        Release(_buffer);
        _buffer = larger;
    }
}
