namespace Rustaveli.Pdf.Fonts;

/// <summary>A growable big-endian byte buffer for building font tables.</summary>
internal sealed class FontDataWriter
{
    private byte[] _buffer;

    public FontDataWriter(int capacity = 256)
    {
        _buffer = new byte[Math.Max(capacity, 16)];
    }

    public int Length { get; private set; }

    public void UInt8(byte value)
    {
        Reserve(1);
        _buffer[Length++] = value;
    }

    public void UInt16(int value)
    {
        Reserve(2);
        BigEndian.WriteUInt16(_buffer, Length, (ushort)value);
        Length += 2;
    }

    public void Int16(int value) => UInt16((ushort)(short)value);

    public void UInt32(uint value)
    {
        Reserve(4);
        BigEndian.WriteUInt32(_buffer, Length, value);
        Length += 4;
    }

    public void Bytes(ReadOnlySpan<byte> data)
    {
        Reserve(data.Length);
        data.CopyTo(_buffer.AsSpan(Length));
        Length += data.Length;
    }

    /// <summary>Appends zeros up to the next multiple of <paramref name="alignment"/>.</summary>
    public void Align(int alignment)
    {
        int padding = (alignment - (Length % alignment)) % alignment;
        Reserve(padding);
        Length += padding;
    }

    /// <summary>Overwrites two bytes already written.</summary>
    public void PatchUInt16(int offset, int value) => BigEndian.WriteUInt16(_buffer, offset, (ushort)value);

    public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();

    private void Reserve(int count)
    {
        if (Length + count <= _buffer.Length)
            return;

        // The new space is zeroed by the allocation, which Align relies on for its padding.
        Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, Length + count));
    }
}
