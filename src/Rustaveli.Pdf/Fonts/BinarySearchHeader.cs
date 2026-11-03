namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The fields a font file writes before an array it lets readers binary-search, such as the table directory or a
/// format 4 <c>cmap</c> subtable's segments.
/// </summary>
/// <param name="SearchRange">The largest power of two not above the count, in bytes.</param>
/// <param name="EntrySelector">That power's exponent.</param>
/// <param name="RangeShift">The bytes of the array past the search range.</param>
internal readonly record struct BinarySearchHeader(int SearchRange, int EntrySelector, int RangeShift)
{
    /// <summary>The fields for <paramref name="count"/> entries of <paramref name="unitSize"/> bytes each.</summary>
    public static BinarySearchHeader For(int count, int unitSize)
    {
        int entrySelector = 0;

        while ((2 << entrySelector) <= count)
            entrySelector++;

        int searchRange = (1 << entrySelector) * unitSize;
        return new BinarySearchHeader(searchRange, entrySelector, (count * unitSize) - searchRange);
    }
}
