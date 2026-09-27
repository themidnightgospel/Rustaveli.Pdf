using System.Globalization;

namespace Rustaveli.Pdf.Writing;

/// <summary>An indirect reference, <c>12 0 R</c>, to a numbered object in the file being written.</summary>
/// <remarks>
/// The generation is always zero: generations only advance when an incremental update reuses a freed object
/// number, and this writer produces new files.
/// </remarks>
internal readonly struct PdfReference : IEquatable<PdfReference>
{
    public PdfReference(int objectNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(objectNumber, 1);
        ObjectNumber = objectNumber;
    }

    /// <summary>The object number; zero only for <c>default</c>, which refers to nothing.</summary>
    public int ObjectNumber { get; }

    public static bool operator ==(PdfReference left, PdfReference right) => left.Equals(right);

    public static bool operator !=(PdfReference left, PdfReference right) => !left.Equals(right);

    public bool Equals(PdfReference other) => ObjectNumber == other.ObjectNumber;

    public override bool Equals(object? obj) => obj is PdfReference other && Equals(other);

    public override int GetHashCode() => ObjectNumber;

    public override string ToString() => ObjectNumber.ToString(CultureInfo.InvariantCulture) + " 0 R";
}
