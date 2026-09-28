namespace Rustaveli.Pdf.Operations.Linearization;

/// <summary>Writes values of any width from 0 to 32 bits, most significant first, as the hint tables pack them.</summary>
internal sealed class BitWriter
{
    private readonly List<byte> _bytes = [];
    private int _current;
    private int _used;

    public int Length => _bytes.Count + (_used > 0 ? 1 : 0);

    public void Write(long value, int bits)
    {
        for (int bit = bits - 1; bit >= 0; bit--)
        {
            _current = (_current << 1) | (int)((value >> bit) & 1);

            if (++_used == 8)
            {
                _bytes.Add((byte)_current);
                _current = 0;
                _used = 0;
            }
        }
    }

    /// <summary>Pads the byte being filled with zero bits, so what follows starts on a byte.</summary>
    public void Align()
    {
        if (_used > 0)
            Write(0, 8 - _used);
    }

    public byte[] ToArray()
    {
        Align();
        return _bytes.ToArray();
    }

    /// <summary>How many bits represent every value up to <paramref name="value"/>: none for zero.</summary>
    public static int Width(long value)
    {
        int bits = 0;

        while (value > 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }
}
