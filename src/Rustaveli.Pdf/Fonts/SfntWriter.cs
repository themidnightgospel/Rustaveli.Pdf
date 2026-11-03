namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Assembles tables into an sfnt font file: the table directory with its binary-search fields, 4-byte aligned table
/// data, per-table checksums and the whole-font checksum adjustment in <c>head</c>.
/// </summary>
internal static class SfntWriter
{
    /// <summary>What the checksum of a whole font must come to once the adjustment is in place.</summary>
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>
    /// The font file of <paramref name="tables"/>, which include <c>head</c>, as every font's tables do. The tables are
    /// only read: they may be another file's own bytes.
    /// </summary>
    public static byte[] Write(uint sfntVersion, IReadOnlyList<KeyValuePair<uint, ReadOnlyMemory<byte>>> tables)
    {
        // The whole file's checksum is settled in head; without one there is nowhere to write it.
        if (!tables.Any(static table => table.Key == TableTag.Head))
            throw new ArgumentException("A font file needs a head table.", nameof(tables));

        KeyValuePair<uint, ReadOnlyMemory<byte>>[] sorted = tables.OrderBy(static table => table.Key).ToArray();
        int count = sorted.Length;
        int directoryLength = TableDirectory.HeaderSize + (count * TableDirectory.RecordSize);
        int length = directoryLength;

        foreach (KeyValuePair<uint, ReadOnlyMemory<byte>> table in sorted)
            length += Padded(table.Value.Length);

        byte[] file = new byte[length];
        BinarySearchHeader search = BinarySearchHeader.For(count, TableDirectory.RecordSize);

        BigEndian.WriteUInt32(file, 0, sfntVersion);
        BigEndian.WriteUInt16(file, 4, (ushort)count);
        BigEndian.WriteUInt16(file, 6, (ushort)search.SearchRange);
        BigEndian.WriteUInt16(file, 8, (ushort)search.EntrySelector);
        BigEndian.WriteUInt16(file, 10, (ushort)search.RangeShift);

        int offset = directoryLength;
        int headOffset = 0;

        for (int index = 0; index < count; index++)
        {
            (uint tag, ReadOnlyMemory<byte> data) = (sorted[index].Key, sorted[index].Value);
            Span<byte> copy = file.AsSpan(offset, data.Length);
            data.Span.CopyTo(copy);

            // The head checksum is taken with its adjustment field zero, which it is in the file until the end.
            if (tag == TableTag.Head)
            {
                headOffset = offset;
                BigEndian.WriteUInt32(file, offset + HeadTable.ChecksumAdjustmentOffset, 0);
            }

            int record = TableDirectory.HeaderSize + (index * TableDirectory.RecordSize);
            BigEndian.WriteUInt32(file, record, tag);
            BigEndian.WriteUInt32(file, record + 4, Checksum(copy));
            BigEndian.WriteUInt32(file, record + 8, (uint)offset);
            BigEndian.WriteUInt32(file, record + 12, (uint)data.Length);

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
