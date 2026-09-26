namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Reads the <c>name</c> table into <see cref="FontNames"/>.
/// </summary>
/// <remarks>
/// A name can appear once per platform, encoding and language. Windows English is preferred because it is what
/// almost every modern font carries and what users type; Unicode-platform and Macintosh English records stand in
/// for fonts made only for those platforms. Records in encodings other than UTF-16 and Mac Roman are skipped rather
/// than decoded wrongly, as are records pointing outside the table, which some otherwise usable fonts contain.
/// </remarks>
internal static class NameTable
{
    public const ushort FamilyId = 1;
    public const ushort SubfamilyId = 2;
    public const ushort FullNameId = 4;
    public const ushort PostScriptNameId = 6;
    public const ushort TypographicFamilyId = 16;
    public const ushort TypographicSubfamilyId = 17;

    private const int RecordSize = 12;
    private const int Unusable = int.MaxValue;

    public static FontNames Read(ReadOnlySpan<byte> data)
    {
        int count = BigEndian.UInt16(data, 2);
        int storage = BigEndian.UInt16(data, 4);

        _ = BigEndian.Slice(data, 6, (long)count * RecordSize);

        string?[] best = new string?[TypographicSubfamilyId + 1];
        int[] bestRank = new int[TypographicSubfamilyId + 1];
        bestRank.AsSpan().Fill(Unusable);
        Aliases family = new Aliases();
        Aliases typographic = new Aliases();

        for (int index = 0; index < count; index++)
        {
            int record = 6 + (index * RecordSize);
            ushort platform = BigEndian.UInt16(data, record);
            ushort encoding = BigEndian.UInt16(data, record + 2);
            ushort language = BigEndian.UInt16(data, record + 4);
            ushort nameId = BigEndian.UInt16(data, record + 6);
            int length = BigEndian.UInt16(data, record + 8);
            int offset = BigEndian.UInt16(data, record + 10);

            if (nameId >= best.Length)
                continue;

            int rank = Rank(platform, encoding, language);
            long start = (long)storage + offset;

            if (rank == Unusable || start + length > data.Length)
                continue;

            string text = Decode(platform, data.Slice((int)start, length));

            if (text.Length == 0)
                continue;

            if (nameId == FamilyId)
                family.Add(text);
            else if (nameId == TypographicFamilyId)
                typographic.Add(text);

            if (rank < bestRank[nameId])
            {
                bestRank[nameId] = rank;
                best[nameId] = text;
            }
        }

        return new FontNames(
            best[FamilyId] ?? string.Empty,
            best[SubfamilyId] ?? string.Empty,
            best[FullNameId] ?? string.Empty,
            best[PostScriptNameId],
            best[TypographicFamilyId],
            best[TypographicSubfamilyId],
            family.Names,
            typographic.Names);
    }

    /// <summary>How preferable a record is, lower being better; <see cref="Unusable"/> if it cannot be decoded.</summary>
    private static int Rank(ushort platform, ushort encoding, ushort language)
    {
        const ushort UnicodePlatform = 0;
        const ushort MacintoshPlatform = 1;
        const ushort WindowsPlatform = 3;
        const ushort EnglishUnitedStates = 0x0409;

        switch (platform)
        {
            // Windows symbol (0), Unicode BMP (1) and full repertoire (10) all store names in UTF-16.
            case WindowsPlatform when encoding is 0 or 1 or 10:
                if (language == EnglishUnitedStates)
                    return 0;

                // Other English locales share the primary language ID in the low ten bits.
                return (language & 0x3FF) == 0x09 ? 1 : 4;

            // Encoding 5 is for variation sequences, which have no names; the rest are UTF-16.
            case UnicodePlatform when encoding != 5:
                return 2;

            // Roman only; the Macintosh CJK encodings would need tables this library does not carry.
            case MacintoshPlatform when encoding == 0:
                return language == 0 ? 3 : 5;

            default:
                return Unusable;
        }
    }

    private static string Decode(ushort platform, ReadOnlySpan<byte> bytes)
    {
        if (platform == 1)
            return MacRoman.Decode(bytes);

        // An odd trailing byte is a truncated character; drop it rather than reject the whole name.
        char[] characters = new char[bytes.Length / 2];

        for (int index = 0; index < characters.Length; index++)
            characters[index] = (char)((bytes[2 * index] << 8) | bytes[(2 * index) + 1]);

        return new string(characters).TrimEnd('\0');
    }

    /// <summary>Distinct names, compared without regard to case, in the order the table lists them.</summary>
    private sealed class Aliases
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public List<string> Names { get; } = [];

        public void Add(string name)
        {
            if (_seen.Add(name))
                Names.Add(name);
        }
    }
}
