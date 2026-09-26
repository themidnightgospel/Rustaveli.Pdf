namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Writes big-endian font structures by hand. Deliberately independent of the library's own writer, so a test
/// building a table cannot share a mistake with the code reading it.
/// </summary>
internal sealed class FontBytes
{
    private readonly List<byte> _bytes = [];

    public int Length => _bytes.Count;

    public FontBytes U8(int value)
    {
        _bytes.Add((byte)value);
        return this;
    }

    public FontBytes U16(int value) => U8(value >> 8).U8(value);

    public FontBytes I16(int value) => U16((ushort)(short)value);

    public FontBytes U24(int value) => U8(value >> 16).U8(value >> 8).U8(value);

    public FontBytes U32(long value) => U16((int)((value >> 16) & 0xFFFF)).U16((int)(value & 0xFFFF));

    public FontBytes Fixed(double value) => U32((long)Math.Round(value * 65536) & 0xFFFFFFFF);

    public FontBytes Tag(string tag)
    {
        foreach (char character in tag)
            U8(character);

        return this;
    }

    public FontBytes Bytes(IEnumerable<byte> bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    public FontBytes Zeros(int count) => Bytes(new byte[count]);

    public FontBytes Utf16(string text)
    {
        foreach (char character in text)
            U16(character);

        return this;
    }

    public FontBytes Align(int alignment)
    {
        while (_bytes.Count % alignment != 0)
            _bytes.Add(0);

        return this;
    }

    public void SetU16(int offset, int value)
    {
        _bytes[offset] = (byte)(value >> 8);
        _bytes[offset + 1] = (byte)value;
    }

    public void SetU32(int offset, long value)
    {
        SetU16(offset, (int)((value >> 16) & 0xFFFF));
        SetU16(offset + 2, (int)(value & 0xFFFF));
    }

    public byte[] ToArray() => _bytes.ToArray();
}
