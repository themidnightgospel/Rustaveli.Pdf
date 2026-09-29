namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Assembles tables into an sfnt font file: the table directory with its binary-search fields, 4-byte aligned table
/// data, per-table checksums and the whole-font checksum adjustment in <c>head</c>.
/// </summary>
internal static class SfntWriter
{
    private const int HeaderSize = 12;
    private const int RecordSize = 16;

    /// <summary>What the checksum of a whole font must come to once the adjustment is in place.</summary>
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>The font file of <paramref name="tables"/>, which include <c>head</c>, as every font's tables do.</summary>
    public static byte[] Write(uint sfntVersion, IReadOnlyList<KeyValuePair<uint, byte[]>> tables)
    {
        KeyValuePair<uint, byte[]>[] sorted = tables.OrderBy(static table => table.Key).ToArray();
        int count = sorted.Length;
        int length = HeaderSize + (count * RecordSize);

        foreach (KeyValuePair<uint, byte[]> table in sorted)
            length += Padded(table.Value.Length);

        byte[] file = new byte[length];
        int entrySelector = 0;

        while ((2 << entrySelector) <= count)
            entrySelector++;

        int searchRange = (1 << entrySelector) * RecordSize;

        BigEndian.WriteUInt32(file, 0, sfntVersion);
        BigEndian.WriteUInt16(file, 4, (ushort)count);
        BigEndian.WriteUInt16(file, 6, (ushort)searchRange);
        BigEndian.WriteUInt16(file, 8, (ushort)entrySelector);
        BigEndian.WriteUInt16(file, 10, (ushort)((count * RecordSize) - searchRange));

        int offset = HeaderSize + (count * RecordSize);
        int headOffset = 0;

        for (int index = 0; index < count; index++)
        {
            (uint tag, byte[] data) = (sorted[index].Key, sorted[index].Value);

            // The head checksum is taken with its adjustment field zero, which it is until the end.
            if (tag == TableTag.Head)
            {
                headOffset = offset;
                BigEndian.WriteUInt32(data, HeadTable.ChecksumAdjustmentOffset, 0);
            }

            int record = HeaderSize + (index * RecordSize);
            BigEndian.WriteUInt32(file, record, tag);
            BigEndian.WriteUInt32(file, record + 4, Checksum(data));
            BigEndian.WriteUInt32(file, record + 8, (uint)offset);
            BigEndian.WriteUInt32(file, record + 12, (uint)data.Length);

            data.CopyTo(file, offset);
            offset += Padded(data.Length);
        }

        uint adjustment = ChecksumMagic - Checksum(file);
        BigEndian.WriteUInt32(file, headOffset + HeadTable.ChecksumAdjustmentOffset, adjustment);

        return file;
    }

    /// <summary>The OpenType checksum: the data summed as big-endian 32-bit words, the last one zero-padded.</summary>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int whole = data.Length & ~3;

        for (int position = 0; position < whole; position += 4)
            sum += BigEndian.UInt32(data, position);

        if (whole < data.Length)
        {
            uint last = 0;

            for (int position = whole; position < data.Length; position++)
                last |= (uint)data[position] << (24 - (8 * (position - whole)));

            sum += last;
        }

        return sum;
    }

    private static int Padded(int length) => (length + 3) & ~3;
}
