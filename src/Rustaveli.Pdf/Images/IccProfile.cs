using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// An ICC colour profile taken from an image, destined for an /ICCBased colour space.
/// </summary>
/// <remarks>
/// Only profiles whose header checks out and whose colour space matches the image are accepted. A PDF names the
/// component count of an ICC-based space separately (/N), and a reader handed a profile that disagrees with it
/// either rejects the image or renders it in the wrong colours; a profile that fails these checks is therefore
/// dropped and the image falls back to the device colour space, which is what browsers do with a bad profile too.
/// </remarks>
internal sealed class IccProfile
{
    private const int HeaderSize = 128;

    private IccProfile(ReadOnlyMemory<byte> data, int componentCount)
    {
        Data = data;
        ComponentCount = componentCount;
        Hash = ImageContentHash.Of(data.Span);
    }

    /// <summary>The profile bytes, uncompressed, trimmed to the size the profile header declares.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>The number of colour components the profile describes: 1 (gray), 3 (RGB) or 4 (CMYK).</summary>
    public int ComponentCount { get; }

    /// <summary>A fingerprint of <see cref="Data"/>, so that images sharing a profile can share one stream.</summary>
    public ImageContentHash Hash { get; }

    /// <summary>
    /// Validates <paramref name="data"/> as a profile for an image with <paramref name="expectedComponents"/>
    /// colour components, returning null if it is not one.
    /// </summary>
    public static IccProfile? TryCreate(ReadOnlyMemory<byte> data, int expectedComponents)
    {
        ReadOnlySpan<byte> span = data.Span;

        // The header, plus the tag count that follows it.
        if (span.Length < HeaderSize + 4)
            return null;

        uint declaredSize = BinaryPrimitives.ReadUInt32BigEndian(span);
        if (declaredSize < HeaderSize + 4 || declaredSize > (uint)span.Length)
            return null;

        if (!span.Slice(36, 4).SequenceEqual("acsp"u8))
            return null;

        int components = ComponentsOf(span.Slice(16, 4));
        if (components != expectedComponents)
            return null;

        return new IccProfile(data.Slice(0, (int)declaredSize), components);
    }

    private static int ComponentsOf(ReadOnlySpan<byte> colorSpace)
    {
        if (colorSpace.SequenceEqual("GRAY"u8))
            return 1;

        if (colorSpace.SequenceEqual("RGB "u8))
            return 3;

        if (colorSpace.SequenceEqual("CMYK"u8))
            return 4;

        return 0;
    }
}
