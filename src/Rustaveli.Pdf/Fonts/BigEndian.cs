namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Bounds-checked big-endian reads and writes of the integer types OpenType is built from.
/// </summary>
/// <remarks>
/// Every read checks its range and reports a short buffer as a <see cref="FontFormatException"/>. The span indexer
/// would throw too, but as an <see cref="IndexOutOfRangeException"/> that callers cannot tell apart from a bug, and
/// malformed fonts are expected input rather than a bug.
/// </remarks>
internal static class BigEndian
{
    public static byte UInt8(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
            throw FontFormatException.Truncated();

        return data[offset];
    }

    public static ushort UInt16(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || data.Length - offset < 2)
            throw FontFormatException.Truncated();

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    public static short Int16(ReadOnlySpan<byte> data, int offset) => (short)UInt16(data, offset);

    public static uint UInt24(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || data.Length - offset < 3)
            throw FontFormatException.Truncated();

        return (uint)((data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2]);
    }

    public static uint UInt32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || data.Length - offset < 4)
            throw FontFormatException.Truncated();

        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) |
               data[offset + 3];
    }

    public static int Int32(ReadOnlySpan<byte> data, int offset) => (int)UInt32(data, offset);

    /// <summary>A 16.16 fixed-point number.</summary>
    public static float Fixed(ReadOnlySpan<byte> data, int offset) => Int32(data, offset) / 65536f;

    /// <summary>
    /// A range of the data, checked to lie wholly inside it. Offsets and lengths are taken as <see cref="long"/> so
    /// that 32-bit values read from the font cannot wrap around before they are checked.
    /// </summary>
    public static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, long offset, long length)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
            throw FontFormatException.Truncated();

        return data.Slice((int)offset, (int)length);
    }

    /// <inheritdoc cref="Slice(ReadOnlySpan{byte}, long, long)"/>
    public static ReadOnlyMemory<byte> Slice(ReadOnlyMemory<byte> data, long offset, long length)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
            throw FontFormatException.Truncated();

        return data.Slice((int)offset, (int)length);
    }

    public static void WriteUInt16(Span<byte> data, int offset, ushort value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }

    public static void WriteUInt32(Span<byte> data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
