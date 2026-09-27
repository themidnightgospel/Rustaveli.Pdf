namespace Rustaveli.Pdf.Images;

/// <summary>
/// The CRC-32 of ISO 3309 / ITU-T V.42 (reflected polynomial 0xEDB88320), which PNG stores after every chunk.
/// </summary>
/// <remarks>
/// Every byte of a PNG passes through here, image data included, so this uses slicing-by-8: eight lookup tables
/// let it consume eight bytes per step instead of one, several times faster than the textbook loop at the cost of
/// 8 KiB of tables.
/// </remarks>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = BuildTable();

    /// <summary>Computes the CRC of <paramref name="data"/> in one call.</summary>
    public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);

    /// <summary>Extends a CRC previously returned by this method (or 0 to start) with more data.</summary>
    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        uint[] table = Table;
        uint state = ~crc;
        int offset = 0;

        for (; offset + 8 <= data.Length; offset += 8)
        {
            uint low = state ^ (data[offset] | ((uint)data[offset + 1] << 8) |
                                ((uint)data[offset + 2] << 16) | ((uint)data[offset + 3] << 24));
            state = table[(7 * 256) + (low & 0xFF)] ^
                    table[(6 * 256) + ((low >> 8) & 0xFF)] ^
                    table[(5 * 256) + ((low >> 16) & 0xFF)] ^
                    table[(4 * 256) + (low >> 24)] ^
                    table[(3 * 256) + data[offset + 4]] ^
                    table[(2 * 256) + data[offset + 5]] ^
                    table[256 + data[offset + 6]] ^
                    table[data[offset + 7]];
        }

        for (; offset < data.Length; offset++)
            state = table[(state ^ data[offset]) & 0xFF] ^ (state >> 8);

        return ~state;
    }

    private static uint[] BuildTable()
    {
        uint[] table = new uint[8 * 256];
        for (uint index = 0; index < 256; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? (value >> 1) ^ Polynomial : value >> 1;
            table[index] = value;
        }

        // Table k advances a byte's contribution through k further zero bytes.
        for (int slice = 1; slice < 8; slice++)
        {
            for (int index = 0; index < 256; index++)
            {
                uint previous = table[((slice - 1) * 256) + index];
                table[(slice * 256) + index] = (previous >> 8) ^ table[previous & 0xFF];
            }
        }

        return table;
    }
}
