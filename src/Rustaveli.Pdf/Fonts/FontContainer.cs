namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The outermost layer of a font file: a single face, or a collection (.ttc, .otc) of faces sharing tables.
/// </summary>
internal static class FontContainer
{
    /// <summary>'ttcf', the tag a collection starts with.</summary>
    public const uint CollectionTag = 0x74746366;

    /// <summary>The collection header before its offset array: tag, version and face count.</summary>
    public const int CollectionHeaderSize = 12;

    private const uint WoffTag = 0x774F4646; // 'wOFF'
    private const uint Woff2Tag = 0x774F4632; // 'wOF2'
    private const uint Type1Tag = 0x74797031; // 'typ1'

    public static bool IsCollection(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && BigEndian.UInt32(data, 0) == CollectionTag;

    /// <summary>The number of faces in the file: one for a plain font file.</summary>
    /// <param name="data">The file, or at least its first <see cref="CollectionHeaderSize"/> bytes.</param>
    public static int CountFaces(ReadOnlySpan<byte> data)
    {
        uint tag = BigEndian.UInt32(data, 0);
        RejectUnsupported(tag);

        if (tag != CollectionTag)
            return 1;

        uint count = BigEndian.UInt32(data, 8);

        if (count == 0)
            throw new FontFormatException("The font collection contains no fonts.");

        // Each face needs a four-byte offset and a twelve-byte header at the very least, so a count the file cannot
        // hold is rejected before anything is sized from it.
        if (count > (uint)(int.MaxValue / 16))
            throw FontFormatException.Truncated();

        return (int)count;
    }

    /// <summary>Where the offset table of face <paramref name="faceIndex"/> starts.</summary>
    /// <param name="data">The file, or at least its header and, for a collection, the whole offset array.</param>
    /// <param name="faceIndex">The zero-based face; must be 0 for a plain font file.</param>
    public static int GetFaceOffset(ReadOnlySpan<byte> data, int faceIndex)
    {
        int count = CountFaces(data);

        if (faceIndex < 0 || faceIndex >= count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex), faceIndex, $"The font file contains {count} face(s).");
        }

        if (!IsCollection(data))
            return 0;

        uint offset = BigEndian.UInt32(data, CollectionHeaderSize + (faceIndex * 4));

        if (offset > int.MaxValue)
            throw FontFormatException.Truncated();

        return (int)offset;
    }

    private static void RejectUnsupported(uint tag)
    {
        if (tag == WoffTag || tag == Woff2Tag)
        {
            throw new FontFormatException(
                "WOFF and WOFF2 web fonts are compressed and not read directly; decompress to OpenType first.");
        }

        if (tag == Type1Tag)
            throw new FontFormatException("PostScript Type 1 fonts are not supported; use an OpenType font.");
    }
}
