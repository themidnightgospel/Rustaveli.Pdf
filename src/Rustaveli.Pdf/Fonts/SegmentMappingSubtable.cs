namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Character map format 4: segments of the Basic Multilingual Plane, each mapped by a delta or through an array of
/// glyph ids. The standard Windows Unicode BMP subtable.
/// </summary>
/// <remarks>
/// The subtable's own 16-bit length field is not trusted: a font whose map outgrows 64 KB wraps it around, and the
/// arrays are found from the segment count instead. Everything to the end of the <c>cmap</c> table is available to
/// glyph-array lookups, which the format addresses relative to the segment rather than the subtable.
/// </remarks>
internal sealed class SegmentMappingSubtable : CharacterMapSubtable
{
    private const int EndCodesOffset = 14;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _segments;
    private readonly int _startCodes;
    private readonly int _deltas;
    private readonly int _rangeOffsets;

    public SegmentMappingSubtable(ReadOnlyMemory<byte> data)
    {
        _data = data;
        _segments = BigEndian.UInt16(data.Span, 6) / 2;

        // End codes, a reserved pad, then start codes, deltas and range offsets: four arrays of the segment count.
        _startCodes = EndCodesOffset + (_segments * 2) + 2;
        _deltas = _startCodes + (_segments * 2);
        _rangeOffsets = _deltas + (_segments * 2);
        _ = BigEndian.Slice(data.Span, 0, _rangeOffsets + (_segments * 2L));
    }

    public override ushort Lookup(int code)
    {
        if (code is < 0 or > 0xFFFF)
            return 0;

        ReadOnlySpan<byte> data = _data.Span;

        // The first segment whose end is at or after the code; segments are sorted by end code.
        int low = 0;
        int high = _segments - 1;

        while (low < high)
        {
            int middle = (low + high) >> 1;

            if (BigEndian.UInt16(data, EndCodesOffset + (middle * 2)) < code)
                low = middle + 1;
            else
                high = middle;
        }

        if (_segments == 0 || BigEndian.UInt16(data, EndCodesOffset + (low * 2)) < code)
            return 0;

        return MapInSegment(data, low, code);
    }

    public override IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings()
    {
        int previousEnd = -1;

        for (int segment = 0; segment < _segments; segment++)
        {
            int end = BigEndian.UInt16(_data.Span, EndCodesOffset + (segment * 2));
            int start = Math.Max(BigEndian.UInt16(_data.Span, _startCodes + (segment * 2)), previousEnd + 1);

            for (int code = start; code <= end; code++)
            {
                ushort glyph = MapInSegment(_data.Span, segment, code);

                if (glyph != 0)
                    yield return new KeyValuePair<int, ushort>(code, glyph);
            }

            previousEnd = Math.Max(previousEnd, end);
        }
    }

    private ushort MapInSegment(ReadOnlySpan<byte> data, int segment, int code)
    {
        int start = BigEndian.UInt16(data, _startCodes + (segment * 2));

        if (code < start)
            return 0;

        ushort delta = BigEndian.UInt16(data, _deltas + (segment * 2));
        int rangeOffsetPosition = _rangeOffsets + (segment * 2);
        int rangeOffset = BigEndian.UInt16(data, rangeOffsetPosition);

        if (rangeOffset == 0)
            return (ushort)(code + delta);

        // The range offset counts from its own position in the range-offset array.
        long position = rangeOffsetPosition + (long)rangeOffset + ((code - start) * 2L);

        if (position + 2 > data.Length)
            return 0;

        ushort glyph = BigEndian.UInt16(data, (int)position);

        return glyph == 0 ? (ushort)0 : (ushort)(glyph + delta);
    }
}
