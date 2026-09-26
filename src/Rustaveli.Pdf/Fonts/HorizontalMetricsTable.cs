namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>hmtx</c> table: each glyph's advance width and left side bearing.
/// </summary>
/// <remarks>
/// The first <c>numberOfHMetrics</c> glyphs carry an advance and a bearing; the rest carry only a bearing and share
/// the last advance, which is how monospaced and CJK fonts avoid repeating one width thousands of times. Reads go
/// straight to the table's bytes: an advance is one bounds-checked 16-bit read, with nothing to allocate or cache.
/// </remarks>
internal sealed class HorizontalMetricsTable
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _metricCount;

    public HorizontalMetricsTable(ReadOnlyMemory<byte> data, int numberOfHMetrics, int glyphCount)
    {
        if (numberOfHMetrics == 0)
            throw new FontFormatException("The font gives no glyph an advance width.");

        // More full metrics than glyphs is malformed but readable: the surplus is never asked for.
        _metricCount = Math.Min(numberOfHMetrics, glyphCount);
        _data = data;
        _ = BigEndian.Slice(data.Span, 0, _metricCount * 4L);
    }

    public ushort GetAdvance(ushort glyph)
    {
        int index = Math.Min(glyph, _metricCount - 1);

        return BigEndian.UInt16(_data.Span, index * 4);
    }

    /// <summary>
    /// The left side bearing, or 0 when the font omits it: many fonts truncate the bearing-only tail, and the value
    /// is recoverable from the glyph outline anyway.
    /// </summary>
    public short GetLeftSideBearing(ushort glyph)
    {
        long position = glyph < _metricCount ? (glyph * 4L) + 2 : (_metricCount * 4L) + ((glyph - _metricCount) * 2L);

        return position + 2 <= _data.Length ? BigEndian.Int16(_data.Span, (int)position) : (short)0;
    }
}
