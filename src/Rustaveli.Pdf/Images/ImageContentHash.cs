using System.Globalization;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// A stable fingerprint of encoded bytes: their XXH64 hash together with their length. Stable across processes,
/// runtimes and machines, so it can also name a cached object.
/// </summary>
/// <remarks>
/// Equal hashes mean "almost certainly the same bytes"; code that must never merge two different images confirms a
/// match by comparing the bytes themselves, as <c>RasterImage.HasSameContent</c> does.
/// </remarks>
internal readonly struct ImageContentHash : IEquatable<ImageContentHash>
{
    public ImageContentHash(ulong value, long length)
    {
        Value = value;
        Length = length;
    }

    /// <summary>The XXH64 hash of the bytes.</summary>
    public ulong Value { get; }

    /// <summary>The number of bytes hashed.</summary>
    public long Length { get; }

    public static bool operator ==(ImageContentHash left, ImageContentHash right) => left.Equals(right);

    public static bool operator !=(ImageContentHash left, ImageContentHash right) => !left.Equals(right);

    public static ImageContentHash Of(ReadOnlySpan<byte> data) => new ImageContentHash(XxHash64.Hash(data), data.Length);

    public bool Equals(ImageContentHash other) => Value == other.Value && Length == other.Length;

    public override bool Equals(object? obj) => obj is ImageContentHash other && Equals(other);

    // The hash is already uniformly distributed, so its low bits serve directly.
    public override int GetHashCode() => unchecked((int)Value);

    public override string ToString() =>
        Value.ToString("x16", CultureInfo.InvariantCulture) + "-" + Length.ToString(CultureInfo.InvariantCulture);
}
