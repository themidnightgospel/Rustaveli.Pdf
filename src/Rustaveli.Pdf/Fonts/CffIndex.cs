namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A CFF INDEX: a counted array of variable-length items, located by an array of 1-based offsets.
/// </summary>
internal readonly struct CffIndex
{
    private readonly int _offsets;
    private readonly int _offsetSize;
    private readonly int _data;

    private CffIndex(int count, int offsets, int offsetSize, int data, int end)
    {
        Count = count;
        _offsets = offsets;
        _offsetSize = offsetSize;
        _data = data;
        End = end;
    }

    public int Count { get; }

    /// <summary>The first byte after the INDEX, where the next structure starts.</summary>
    public int End { get; }

    public static CffIndex Read(ReadOnlySpan<byte> cff, int position)
    {
        int count = BigEndian.UInt16(cff, position);

        // An empty INDEX is its count alone.
        if (count == 0)
            return new CffIndex(0, position + 2, 1, position + 2, position + 2);

        int offsetSize = BigEndian.UInt8(cff, position + 2);

        if (offsetSize is < 1 or > 4)
            throw new FontFormatException($"A CFF INDEX cannot have {offsetSize}-byte offsets.");

        int offsets = position + 3;
        long data = offsets + ((count + 1L) * offsetSize) - 1;
        _ = BigEndian.Slice(cff, offsets, (count + 1L) * offsetSize);

        CffIndex index = new CffIndex(count, offsets, offsetSize, (int)data, 0);
        long end = data + index.OffsetAt(cff, count);

        if (end > cff.Length)
            throw FontFormatException.Truncated();

        return new CffIndex(count, offsets, offsetSize, (int)data, (int)end);
    }

    /// <summary>The position and length of item <paramref name="item"/> within the CFF data.</summary>
    public (int Start, int Length) GetItem(ReadOnlySpan<byte> cff, int item)
    {
        if (item < 0 || item >= Count)
            throw new FontFormatException($"The CFF INDEX has no item {item}.");

        uint start = OffsetAt(cff, item);
        uint end = OffsetAt(cff, item + 1);

        // Offsets are 1-based from the byte before the data, so 0 is never valid, and they may not decrease.
        if (start == 0 || end < start || _data + (long)end > End)
            throw new FontFormatException("A CFF INDEX has offsets out of order.");

        return (_data + (int)start, (int)(end - start));
    }

    private uint OffsetAt(ReadOnlySpan<byte> cff, int item)
    {
        int position = _offsets + (item * _offsetSize);

        return _offsetSize switch
        {
            1 => BigEndian.UInt8(cff, position),
            2 => BigEndian.UInt16(cff, position),
            3 => BigEndian.UInt24(cff, position),
            _ => BigEndian.UInt32(cff, position)
        };
    }
}
