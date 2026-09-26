namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The table directory of one font face: which tables it has and where each one lies in the file.
/// </summary>
internal sealed class TableDirectory
{
    /// <summary>The sfnt version of TrueType outlines.</summary>
    public const uint TrueTypeVersion = 0x00010000;

    /// <summary>The sfnt version of CFF outlines, 'OTTO'.</summary>
    public const uint CffVersion = 0x4F54544F;

    /// <summary>The sfnt version Apple uses for TrueType outlines, 'true'.</summary>
    public const uint AppleTrueTypeVersion = 0x74727565;

    private const int HeaderSize = 12;
    private const int RecordSize = 16;

    private readonly TableRecord[] _records;

    private TableDirectory(uint sfntVersion, TableRecord[] records)
    {
        SfntVersion = sfntVersion;
        _records = records;
    }

    public uint SfntVersion { get; }

    public IReadOnlyList<TableRecord> Records => _records;

    /// <summary>
    /// The outline format, from the tables present rather than the sfnt version, which some fonts get wrong.
    /// </summary>
    public OutlineFormat Outlines =>
        Contains(TableTag.Glyf) ? OutlineFormat.TrueType
        : Contains(TableTag.Cff) ? OutlineFormat.Cff
        : Contains(TableTag.Cff2) ? OutlineFormat.Cff2
        : OutlineFormat.None;

    /// <summary>
    /// Reads the directory of the face starting at <paramref name="faceOffset"/>.
    /// </summary>
    /// <param name="file">The whole file: table offsets are relative to its start, even inside a collection.</param>
    /// <param name="faceOffset">Where the face's offset table starts.</param>
    /// <param name="fileLength">
    /// The length of the whole file, which may be longer than <paramref name="file"/> when only the directory has
    /// been read from disk. Every table must lie inside it.
    /// </param>
    public static TableDirectory Read(ReadOnlySpan<byte> file, int faceOffset, long fileLength)
    {
        uint version = BigEndian.UInt32(file, faceOffset);

        if (version != TrueTypeVersion && version != CffVersion && version != AppleTrueTypeVersion)
            throw new FontFormatException($"'{TableTag.ToString(version)}' is not an OpenType font version.");

        int count = BigEndian.UInt16(file, faceOffset + 4);

        // The whole directory is checked before any of it is allocated for, so a count of 65 535 in a 20-byte file
        // fails here instead of reserving a megabyte of records.
        _ = BigEndian.Slice(file, faceOffset + (long)HeaderSize, (long)count * RecordSize);

        TableRecord[] records = new TableRecord[count];

        for (int index = 0; index < count; index++)
        {
            int position = faceOffset + HeaderSize + (index * RecordSize);
            uint tag = BigEndian.UInt32(file, position);
            uint checksum = BigEndian.UInt32(file, position + 4);
            uint offset = BigEndian.UInt32(file, position + 8);
            uint length = BigEndian.UInt32(file, position + 12);

            if ((long)offset + length > fileLength)
                throw new FontFormatException($"The '{TableTag.ToString(tag)}' table runs past the end of the file.");

            records[index] = new TableRecord(tag, checksum, (int)offset, (int)length);
        }

        return new TableDirectory(version, SortWithoutDuplicates(records));
    }

    /// <summary>
    /// Sorts records by tag for binary search, keeping only the first of any duplicated tag.
    /// </summary>
    /// <remarks>
    /// A duplicated tag is malformed, but harmless as long as one copy is used consistently. Sorting first keeps
    /// the de-duplication linear: a pairwise search would be quadratic in a count the file controls.
    /// </remarks>
    private static TableRecord[] SortWithoutDuplicates(TableRecord[] records)
    {
        int[] order = new int[records.Length];

        for (int index = 0; index < order.Length; index++)
            order[index] = index;

        // Ties broken by position, so "first" means first in the file whatever the sort algorithm does.
        Array.Sort(order, (left, right) =>
        {
            int byTag = records[left].Tag.CompareTo(records[right].Tag);
            return byTag != 0 ? byTag : left.CompareTo(right);
        });

        List<TableRecord> unique = new List<TableRecord>(records.Length);

        foreach (int index in order)
        {
            if (unique.Count == 0 || unique[unique.Count - 1].Tag != records[index].Tag)
                unique.Add(records[index]);
        }

        return unique.ToArray();
    }

    public bool Contains(uint tag) => TryGet(tag, out _);

    public bool TryGet(uint tag, out TableRecord record)
    {
        int low = 0;
        int high = _records.Length - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            uint candidate = _records[middle].Tag;

            if (candidate == tag)
            {
                record = _records[middle];
                return true;
            }

            if (candidate < tag)
                low = middle + 1;
            else
                high = middle - 1;
        }

        record = default;
        return false;
    }
}
