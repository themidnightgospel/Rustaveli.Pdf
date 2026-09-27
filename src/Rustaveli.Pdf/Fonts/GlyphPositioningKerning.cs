namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Kerning from the GPOS table: the pair adjustment lookups of the <c>kern</c> feature.
/// </summary>
/// <remarks>
/// <para>
/// Text is measured without first being split by script, so the lookups are those the <c>kern</c> feature names in
/// the default language system of every script, merged — the default script's, and Latin's, Cyrillic's, Georgian's
/// and so on. In practice fonts point every script at the same lookups, or at lookups over disjoint glyphs; merging
/// them kerns each script as a shaper would.
/// </para>
/// <para>
/// Lookups apply in lookup-list order and their adjustments add up, as they do in a shaper; within one lookup the
/// first subtable that applies to a pair wins. Pair adjustments reached through extension lookups (type 9) are
/// followed. Lookup flags that skip marks are not: a mark between two letters is simply a different pair.
/// </para>
/// </remarks>
internal sealed class GlyphPositioningKerning : KerningSource
{
    /// <summary>
    /// More pair subtables than any real font carries. Measuring consults every one of them for every pair, so an
    /// adversarial font listing a subtable hundreds of thousands of times would make each measurement crawl.
    /// </summary>
    private const int MaximumSubtables = 4096;

    /// <summary>
    /// The most list entries — scripts, features, lookups, subtables — read while finding the kerning lookups. Real
    /// fonts need a few thousand. The lists nest and may share entries, so a small table could otherwise declare
    /// billions of them.
    /// </summary>
    private const int MaximumEntries = 1 << 18;

    private const int PairAdjustmentType = 2;
    private const int ExtensionType = 9;

    private readonly PairAdjustment[][] _lookups;
    private int _entryBudget = MaximumEntries;

    public GlyphPositioningKerning(ReadOnlyMemory<byte> table)
    {
        ReadOnlySpan<byte> span = table.Span;
        int scriptList = BigEndian.UInt16(span, 4);
        int featureList = BigEndian.UInt16(span, 6);
        int lookupList = BigEndian.UInt16(span, 8);

        if (scriptList == 0 || featureList == 0 || lookupList == 0)
        {
            _lookups = [];
            return;
        }

        SortedSet<int> lookups = KernLookups(span, featureList, DefaultFeatures(span, scriptList));
        _lookups = ReadLookups(table, lookupList, lookups);
    }

    public bool HasPairs => _lookups.Length > 0;

    public override int GetAdjustment(ushort left, ushort right)
    {
        int total = 0;

        foreach (PairAdjustment[] lookup in _lookups)
        {
            foreach (PairAdjustment subtable in lookup)
            {
                if (subtable.TryGetAdjustment(left, right, out int adjustment))
                {
                    total += adjustment;
                    break;
                }
            }
        }

        return total;
    }

    /// <summary>The features of every script's default language system, the required feature included.</summary>
    private HashSet<int> DefaultFeatures(ReadOnlySpan<byte> span, int scriptList)
    {
        HashSet<int> features = new HashSet<int>();
        int scriptCount = Spend(BigEndian.UInt16(span, scriptList));

        for (int script = 0; script < scriptCount; script++)
        {
            int scriptTable = scriptList + BigEndian.UInt16(span, scriptList + 2 + (script * 6) + 4);
            int defaultLanguage = BigEndian.UInt16(span, scriptTable);

            if (defaultLanguage == 0)
                continue;

            int languageSystem = scriptTable + defaultLanguage;
            ushort required = BigEndian.UInt16(span, languageSystem + 2);

            if (required != 0xFFFF)
                features.Add(required);

            int featureCount = Spend(BigEndian.UInt16(span, languageSystem + 4));

            for (int feature = 0; feature < featureCount; feature++)
                features.Add(BigEndian.UInt16(span, languageSystem + 6 + (feature * 2)));
        }

        return features;
    }

    /// <summary>The lookups of those features that are <c>kern</c>, in the order a shaper applies them.</summary>
    private SortedSet<int> KernLookups(ReadOnlySpan<byte> span, int featureList, HashSet<int> features)
    {
        SortedSet<int> lookups = new SortedSet<int>();
        int featureCount = BigEndian.UInt16(span, featureList);

        foreach (int feature in features)
        {
            // An index past the list is malformed; the feature it names cannot be found, so it adds nothing.
            if (feature >= featureCount)
                continue;

            int record = featureList + 2 + (feature * 6);

            if (BigEndian.UInt32(span, record) != TableTag.Kern)
                continue;

            int featureTable = featureList + BigEndian.UInt16(span, record + 4);
            int lookupCount = Spend(BigEndian.UInt16(span, featureTable + 2));

            for (int lookup = 0; lookup < lookupCount; lookup++)
                lookups.Add(BigEndian.UInt16(span, featureTable + 4 + (lookup * 2)));
        }

        return lookups;
    }

    private PairAdjustment[][] ReadLookups(ReadOnlyMemory<byte> table, int lookupList, SortedSet<int> indices)
    {
        ReadOnlySpan<byte> span = table.Span;
        int lookupCount = BigEndian.UInt16(span, lookupList);
        List<PairAdjustment[]> lookups = new List<PairAdjustment[]>();

        // Fonts share subtables between lookups; each is read once however often it is referenced.
        Dictionary<int, PairAdjustment> read = new Dictionary<int, PairAdjustment>();
        int total = 0;

        foreach (int index in indices)
        {
            if (index >= lookupCount)
                continue;

            int lookup = lookupList + BigEndian.UInt16(span, lookupList + 2 + (index * 2));
            int type = BigEndian.UInt16(span, lookup);
            int subtableCount = Spend(BigEndian.UInt16(span, lookup + 4));
            List<PairAdjustment> subtables = new List<PairAdjustment>();

            for (int subtable = 0; subtable < subtableCount; subtable++)
            {
                int offset = lookup + BigEndian.UInt16(span, lookup + 6 + (subtable * 2));

                if (ResolvePairSubtable(span, type, offset) is not int position)
                    continue;

                if (++total > MaximumSubtables)
                    throw new FontFormatException("The GPOS table has more kerning subtables than a font can use.");

                if (!read.TryGetValue(position, out PairAdjustment? adjustment))
                {
                    adjustment = PairAdjustment.Read(table, position);
                    read.Add(position, adjustment);
                }

                subtables.Add(adjustment);
            }

            if (subtables.Count > 0)
                lookups.Add(subtables.ToArray());
        }

        return lookups.ToArray();
    }

    /// <summary>
    /// Where the pair adjustment subtable is, following an extension lookup's 32-bit offset; null for a subtable of
    /// another type, which the kern feature may also list.
    /// </summary>
    private static int? ResolvePairSubtable(ReadOnlySpan<byte> span, int type, int offset)
    {
        if (type == PairAdjustmentType)
            return offset;

        if (type != ExtensionType || BigEndian.UInt16(span, offset + 2) != PairAdjustmentType)
            return null;

        long target = offset + (long)BigEndian.UInt32(span, offset + 4);

        if (target >= span.Length)
            throw FontFormatException.Truncated();

        return (int)target;
    }

    /// <summary>Accounts for a list about to be read, failing once the font has declared implausibly many.</summary>
    private int Spend(int entries)
    {
        _entryBudget -= entries;

        if (_entryBudget < 0)
            throw new FontFormatException("The GPOS table declares more entries than a font can use.");

        return entries;
    }
}
