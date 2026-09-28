using System.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A PDF name object, such as <c>/Type</c>. Immutable, and equal to any other name with the same value.
/// </summary>
/// <remarks>
/// The serialised form, escapes included, is computed once at construction. Names recur constantly — every
/// dictionary key is one — so paying for encoding on each write would repeat the same work thousands of times.
/// </remarks>
internal sealed class PdfName : IEquatable<PdfName>
{
    private static readonly Encoding Strict = new UTF8Encoding(false, throwOnInvalidBytes: true);

    private readonly byte[] _encoded;

    /// <summary>
    /// Creates a name from its value, without the leading solidus. Non-ASCII text is stored as UTF-8.
    /// </summary>
    public PdfName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("A PDF name cannot contain the null character.", nameof(value));

        Value = value;
        _encoded = Encode(Encoding.UTF8.GetBytes(value));
    }

    private PdfName(string value, byte[] encoded)
    {
        Value = value;
        _encoded = encoded;
    }

    public string Value { get; }

    /// <summary>
    /// A name read from a file as <paramref name="bytes"/>, its escapes already undone, written back byte for byte. A
    /// name in UTF-8 is the same name its text makes; any other bytes are taken one character each.
    /// </summary>
    public static PdfName FromBytes(ReadOnlySpan<byte> bytes)
    {
        byte[] raw = bytes.ToArray();

        if (Array.IndexOf(raw, (byte)0) >= 0)
            throw new ArgumentException("A PDF name cannot contain the null byte.", nameof(bytes));

        try
        {
            return new PdfName(Strict.GetString(raw), Encode(raw));
        }
        catch (DecoderFallbackException)
        {
            return new PdfName(new string(raw.Select(value => (char)value).ToArray()), Encode(raw));
        }
    }

    /// <summary>
    /// The name as it appears in a file: the solidus, then each byte either as itself or as <c>#xx</c>.
    /// </summary>
    public ReadOnlySpan<byte> Encoded => _encoded;

    public bool Equals(PdfName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PdfName);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>The serialised form, for diagnostics.</summary>
    public override string ToString() => Encoding.ASCII.GetString(_encoded);

    private static byte[] Encode(byte[] bytes)
    {
        int length = 1;
        foreach (byte value in bytes)
            length += PdfCharacters.IsNameCharacter(value) ? 1 : 3;

        byte[] encoded = new byte[length];
        encoded[0] = (byte)'/';

        int position = 1;
        foreach (byte value in bytes)
        {
            if (PdfCharacters.IsNameCharacter(value))
            {
                encoded[position++] = value;
            }
            else
            {
                encoded[position++] = (byte)'#';
                encoded[position++] = PdfCharacters.HexDigit(value >> 4);
                encoded[position++] = PdfCharacters.HexDigit(value);
            }
        }

        return encoded;
    }
}
