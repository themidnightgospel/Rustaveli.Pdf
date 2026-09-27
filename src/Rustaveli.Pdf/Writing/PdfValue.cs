namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A direct PDF object: null, a boolean, an integer, a real, a name, a string, an array, a dictionary or an
/// indirect reference. Converts implicitly from each of those, so dictionaries and arrays read as literals.
/// </summary>
/// <remarks>
/// A struct rather than a class hierarchy because documents are mostly numbers — coordinates, widths, offsets —
/// and giving each its own heap object would make allocation grow with content. Scalars live in <c>_bits</c>;
/// the composite kinds keep their object in <c>_object</c>.
/// </remarks>
internal readonly struct PdfValue
{
    private readonly object? _object;
    private readonly long _bits;

    private PdfValue(PdfValueKind kind, long bits, object? value)
    {
        Kind = kind;
        _bits = bits;
        _object = value;
    }

    /// <summary>The PDF null object. Also what <c>default</c> holds.</summary>
    public static PdfValue Null => default;

    public PdfValueKind Kind { get; }

    public static implicit operator PdfValue(bool value) => new PdfValue(PdfValueKind.Boolean, value ? 1 : 0, null);

    public static implicit operator PdfValue(int value) => new PdfValue(PdfValueKind.Integer, value, null);

    public static implicit operator PdfValue(long value) => new PdfValue(PdfValueKind.Integer, value, null);

    public static implicit operator PdfValue(double value) =>
        new PdfValue(PdfValueKind.Real, BitConverter.DoubleToInt64Bits(value), null);

    public static implicit operator PdfValue(PdfName value) => FromObject(PdfValueKind.Name, value);

    public static implicit operator PdfValue(PdfString value) => FromObject(PdfValueKind.String, value);

    public static implicit operator PdfValue(PdfArray value) => FromObject(PdfValueKind.Array, value);

    public static implicit operator PdfValue(PdfDictionary value) => FromObject(PdfValueKind.Dictionary, value);

    public static implicit operator PdfValue(PdfReference value)
    {
        // default(PdfReference) numbers no object; writing "0 0 R" would produce a reference nothing can resolve.
        if (value.ObjectNumber == 0)
            throw new ArgumentException("The reference was never assigned an object number.", nameof(value));

        return new PdfValue(PdfValueKind.Reference, value.ObjectNumber, null);
    }

    /// <summary>
    /// The text a real was read as, when it was read from a file, so it is written back exactly rather than at the
    /// precision the writer gives the reals it formats itself.
    /// </summary>
    public byte[]? RealText => Kind == PdfValueKind.Real ? _object as byte[] : null;

    /// <summary>A real read from a file as <paramref name="text"/>, written back as it was read.</summary>
    public static PdfValue ReadReal(double value, byte[] text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new PdfValue(PdfValueKind.Real, BitConverter.DoubleToInt64Bits(value), text);
    }

    public bool AsBoolean() => Kind == PdfValueKind.Boolean ? _bits != 0 : throw Mismatch(PdfValueKind.Boolean);

    public long AsInteger() => Kind == PdfValueKind.Integer ? _bits : throw Mismatch(PdfValueKind.Integer);

    public double AsReal() =>
        Kind == PdfValueKind.Real ? BitConverter.Int64BitsToDouble(_bits) : throw Mismatch(PdfValueKind.Real);

    public PdfName AsName() => Kind == PdfValueKind.Name ? (PdfName)_object! : throw Mismatch(PdfValueKind.Name);

    public PdfString AsString() =>
        Kind == PdfValueKind.String ? (PdfString)_object! : throw Mismatch(PdfValueKind.String);

    public PdfArray AsArray() => Kind == PdfValueKind.Array ? (PdfArray)_object! : throw Mismatch(PdfValueKind.Array);

    public PdfDictionary AsDictionary() =>
        Kind == PdfValueKind.Dictionary ? (PdfDictionary)_object! : throw Mismatch(PdfValueKind.Dictionary);

    public PdfReference AsReference() =>
        Kind == PdfValueKind.Reference ? new PdfReference((int)_bits) : throw Mismatch(PdfValueKind.Reference);

    private static PdfValue FromObject(PdfValueKind kind, object? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PdfValue(kind, 0, value);
    }

    private InvalidOperationException Mismatch(PdfValueKind requested) =>
        new InvalidOperationException($"The value is {Kind}, not {requested}.");
}
