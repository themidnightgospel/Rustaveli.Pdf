namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// The GSUB table: the substitutions a font makes for ligatures, contextual forms, small capitals, figure styles
/// and the like, applied to a buffer of glyphs for a script, a language and a set of features.
/// </summary>
/// <remarks>
/// <para>
/// The script's language system chooses the features: the requested script, else the default script
/// (<c>DFLT</c>), else Latin; within it the requested language, else the script's default language system. Its
/// required feature always applies; the others apply when the settings turn them on. Every lookup of every applying
/// feature then runs over the whole buffer, one lookup at a time in lookup-list order, which is the order the font
/// was designed to be applied in whatever order the features are listed.
/// </para>
/// <para>
/// All eight lookup types are applied, contextual ones in all three formats, with extension lookups followed. The
/// lookup flags that pass over base glyphs, ligatures and marks — by class, attachment type or mark filtering set —
/// are honoured using the font's GDEF table; right-to-left, which concerns only cursive positioning, has no
/// meaning here. Feature variations, which swap lookups in at particular axis settings of a variable font, are not
/// read: the default instance's lookups apply.
/// </para>
/// <para>
/// Lookups are parsed on first use and kept; threads racing to parse one may each do so, and one result is kept, so
/// one table may shape text on several threads at once without a lock on the path every glyph takes.
/// Malformed script, feature and lookup lists throw <see cref="FontFormatException"/> from whichever call first
/// reads them; a malformed lookup is read as far as it can be, and a subtable of it that cannot be read is left out,
/// so the rest of the font's substitutions still apply. Damage that only shows while applying a subtable throws.
/// </para>
/// </remarks>
internal sealed class GlyphSubstitutionTable
{
    private const int ExtensionType = 7;

    /// <summary>
    /// The most list entries — features and their lookup indices — read while choosing the lookups for one call.
    /// Real fonts need a few hundred; lists nest and may share entries, so a small table could declare billions.
    /// </summary>
    private const int MaximumEntries = 1 << 18;

    /// <summary>
    /// The most subtables the table's lookups may declare between them. Real fonts carry a few thousand at most; each
    /// is an object, so a table listing one subtable in every lookup tens of thousands of times must not become
    /// billions of them.
    /// </summary>
    private const int MaximumSubtables = 1 << 18;

    /// <summary>What a lookup the font got wrong is read as: one that substitutes nothing.</summary>
    private static readonly SubstitutionLookup Nothing = new SubstitutionLookup(0, LookupFlags.None, [], null, null);

    private readonly ReadOnlyMemory<byte> _table;
    private readonly GlyphDefinitionTable? _definitions;
    private readonly int _scriptList;
    private readonly int _featureList;
    private readonly int _lookupList;
    private readonly SubstitutionLookup?[] _lookups;
    private int _subtableBudget = MaximumSubtables;

    /// <summary>Reads a font's GSUB table: its features, and the lookups they name, read as they are first used.</summary>
    /// <param name="table">The GSUB table's bytes.</param>
    /// <param name="definitions">The font's GDEF table, for the lookup flags; null when it has none.</param>
    /// <param name="glyphCount">How many glyphs the font has; a substitution to any other glyph is not made.</param>
    public GlyphSubstitutionTable(ReadOnlyMemory<byte> table, GlyphDefinitionTable? definitions, int glyphCount)
    {
        ReadOnlySpan<byte> span = table.Span;
        int major = BigEndian.UInt16(span, 0);
        int minor = BigEndian.UInt16(span, 2);

        if (major != 1)
            throw new FontFormatException($"GSUB version {major}.{minor} does not exist.");

        _table = table;
        _definitions = definitions;
        GlyphCount = glyphCount;
        _scriptList = BigEndian.UInt16(span, 4);
        _featureList = BigEndian.UInt16(span, 6);
        _lookupList = BigEndian.UInt16(span, 8);

        int lookupCount = _lookupList == 0 ? 0 : BigEndian.UInt16(span, _lookupList);
        _ = BigEndian.Slice(span, _lookupList + 2L, lookupCount * 2L);
        _lookups = new SubstitutionLookup?[lookupCount];
    }

    /// <summary>
    /// The features a shaper turns on for horizontal text in scripts without shaping rules of their own — Latin,
    /// Greek, Cyrillic, Georgian and the like: composition, localized forms, and required, contextual and standard
    /// ligatures.
    /// </summary>
    public static IReadOnlyList<FeatureSetting> DefaultFeatures { get; } =
    [
        FeatureSetting.On(FeatureTag.RequiredVariationAlternates),
        FeatureSetting.On(FeatureTag.GlyphComposition),
        FeatureSetting.On(FeatureTag.LocalizedForms),
        FeatureSetting.On(FeatureTag.RequiredLigatures),
        FeatureSetting.On(FeatureTag.ContextualAlternates),
        FeatureSetting.On(FeatureTag.ContextualLigatures),
        FeatureSetting.On(FeatureTag.StandardLigatures)
    ];

    public int GlyphCount { get; }

    public int LookupCount => _lookups.Length;

    /// <summary>
    /// Applies the lookups the features call for to every glyph of the buffer, editing it in place.
    /// </summary>
    /// <param name="buffer">The glyphs, in logical order.</param>
    /// <param name="script">The script of the text.</param>
    /// <param name="language">The language of the text, or <see cref="LanguageTag.Default"/>.</param>
    /// <param name="features">
    /// Which features to apply. Where several settings name one feature, the last decides, so overrides can follow
    /// a list of defaults such as <see cref="DefaultFeatures"/>.
    /// </param>
    public void Apply(
        GlyphBuffer buffer, ScriptTag script, LanguageTag language, IReadOnlyList<FeatureSetting> features)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Apply(new SubstitutionSession(this, buffer), ResolveLookups(script, language, features));
    }

    /// <summary>Applies lookups, each with the value of the feature that called for it, in the order given.</summary>
    public void Apply(SubstitutionSession session, IReadOnlyList<(int Index, int Value)> lookups)
    {
        // Indexed rather than enumerated: every run measured comes through here, and an enumerator each would add up.
        for (int at = 0; at < lookups.Count; at++)
        {
            (int index, int value) = lookups[at];

            if (GetLookup(index) is not SubstitutionLookup lookup)
                continue;

            session.FeatureValue = value;
            lookup.Apply(session);
        }
    }

    /// <summary>
    /// The lookups the features call for, in the order they apply, each with the value of the feature that called
    /// for it — the required feature's being 1. A lookup two features call for takes the value of the one the
    /// language system lists later.
    /// </summary>
    public IReadOnlyList<(int Index, int Value)> ResolveLookups(
        ScriptTag script, LanguageTag language, IReadOnlyList<FeatureSetting> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        ReadOnlySpan<byte> span = _table.Span;
        int languageSystem = _scriptList == 0 || _featureList == 0 ? -1 : FindLanguageSystem(span, script, language);

        if (languageSystem < 0)
            return [];

        Dictionary<uint, FeatureSetting> settings = new Dictionary<uint, FeatureSetting>();

        foreach (FeatureSetting setting in features)
            settings[setting.Tag.Value] = setting;

        SortedDictionary<int, int> lookups = new SortedDictionary<int, int>();
        int featureCount = BigEndian.UInt16(span, _featureList);
        int budget = MaximumEntries;
        int required = BigEndian.UInt16(span, languageSystem + 2);

        // 0xFFFF, which means there is none, is past the end of any feature list.
        if (required < featureCount)
            AddLookups(span, required, 1, lookups, ref budget);

        int count = Spend(ref budget, BigEndian.UInt16(span, languageSystem + 4));

        for (int entry = 0; entry < count; entry++)
        {
            int feature = BigEndian.UInt16(span, languageSystem + 6 + (entry * 2));

            // An index past the feature list names no feature, so it adds nothing.
            if (feature >= featureCount)
                continue;

            uint tag = BigEndian.UInt32(span, _featureList + 2 + (feature * 6));

            if (settings.TryGetValue(tag, out FeatureSetting setting) && setting.IsEnabled)
                AddLookups(span, feature, setting.Value, lookups, ref budget);
        }

        return lookups.Select(pair => (pair.Key, pair.Value)).ToArray();
    }

    /// <summary>Lookup <paramref name="index"/> of the lookup list, parsed on first use; null past the list.</summary>
    public SubstitutionLookup? GetLookup(int index)
    {
        if (index >= _lookups.Length)
            return null;

        SubstitutionLookup? lookup = Volatile.Read(ref _lookups[index]);

        if (lookup is not null)
            return lookup;

        // A lookup is immutable once read, so a thread that loses the race uses the winner's and drops its own.
        return Interlocked.CompareExchange(ref _lookups[index], ReadLookup(index), null) ?? _lookups[index];
    }

    private static int Spend(ref int budget, int entries)
    {
        budget -= entries;

        if (budget < 0)
            throw new FontFormatException("The GSUB table declares more entries than a font can use.");

        return entries;
    }

    private int FindLanguageSystem(ReadOnlySpan<byte> span, ScriptTag script, LanguageTag language)
    {
        int scriptTable = FindScript(span, script);

        if (scriptTable < 0)
            scriptTable = FindScript(span, ScriptTag.Default);

        if (scriptTable < 0)
            scriptTable = FindScript(span, ScriptTag.Latin);

        if (scriptTable < 0)
            return -1;

        int languageCount = BigEndian.UInt16(span, scriptTable + 2);

        for (int entry = 0; entry < languageCount; entry++)
        {
            int record = scriptTable + 4 + (entry * 6);

            if (BigEndian.UInt32(span, record) == language.Value)
                return scriptTable + BigEndian.UInt16(span, record + 4);
        }

        int defaultLanguage = BigEndian.UInt16(span, scriptTable);
        return defaultLanguage == 0 ? -1 : scriptTable + defaultLanguage;
    }

    private int FindScript(ReadOnlySpan<byte> span, ScriptTag script)
    {
        int count = BigEndian.UInt16(span, _scriptList);

        for (int entry = 0; entry < count; entry++)
        {
            int record = _scriptList + 2 + (entry * 6);

            if (BigEndian.UInt32(span, record) == script.Value)
                return _scriptList + BigEndian.UInt16(span, record + 4);
        }

        return -1;
    }

    private void AddLookups(
        ReadOnlySpan<byte> span, int feature, int value, SortedDictionary<int, int> lookups, ref int budget)
    {
        int table = _featureList + BigEndian.UInt16(span, _featureList + 2 + (feature * 6) + 4);
        int count = Spend(ref budget, BigEndian.UInt16(span, table + 2));

        for (int entry = 0; entry < count; entry++)
        {
            int lookup = BigEndian.UInt16(span, table + 4 + (entry * 2));

            if (lookup < _lookups.Length)
                lookups[lookup] = value;
        }
    }

    /// <summary>
    /// Reads a lookup, leaving out what the font got wrong: a subtable that cannot be read is dropped and the rest
    /// kept, and a lookup whose own header cannot be read, or that would take more subtables than the allowance has
    /// left, substitutes nothing. That is what HarfBuzz makes of the same damage, and it is kept like any other
    /// lookup — read again at every run, a malformed lookup would spend the allowance each time and in the end leave
    /// every lookup not yet read unreadable, so what a font set would depend on how much text came before it.
    /// </summary>
    private SubstitutionLookup ReadLookup(int index)
    {
        ReadOnlySpan<byte> span = _table.Span;
        int lookup;
        int type;
        LookupFlags flags;
        int count;
        CoverageTable? markFilteringSet;

        try
        {
            lookup = _lookupList + BigEndian.UInt16(span, _lookupList + 2 + (index * 2));
            type = BigEndian.UInt16(span, lookup);
            flags = (LookupFlags)BigEndian.UInt16(span, lookup + 2);
            count = BigEndian.UInt16(span, lookup + 4);
            _ = BigEndian.Slice(span, lookup + 6L, count * 2L);

            markFilteringSet = (flags & LookupFlags.UseMarkFilteringSet) != 0
                ? _definitions?.GetMarkGlyphSet(BigEndian.UInt16(span, lookup + 6 + (count * 2)))
                : null;
        }
        catch (FontFormatException)
        {
            return Nothing;
        }

        // Threads may read the same lookup at once, so the allowance is spent atomically.
        if (Interlocked.Add(ref _subtableBudget, -count) < 0)
            return Nothing;

        List<SubstitutionSubtable> subtables = new List<SubstitutionSubtable>(count);
        int lookupType = type;

        for (int subtable = 0; subtable < count; subtable++)
        {
            int offset = lookup + BigEndian.UInt16(span, lookup + 6 + (subtable * 2));
            int subtableType = type;

            try
            {
                if (type == ExtensionType)
                {
                    subtableType = BigEndian.UInt16(span, offset + 2);

                    // All of an extension lookup's subtables have the same type, which is what decides its direction.
                    if (subtable == 0)
                        lookupType = subtableType;

                    offset = ResolveExtension(span, offset, subtableType);
                }

                subtables.Add(SubstitutionSubtable.Read(_table, subtableType, offset));
            }
            catch (FontFormatException)
            {
                // Left out, as HarfBuzz leaves out a subtable that fails its checks; the lookup's others still apply.
            }
        }

        return new SubstitutionLookup(lookupType, flags, [.. subtables], _definitions, markFilteringSet);
    }

    /// <summary>Where an extension subtable's 32-bit offset leads.</summary>
    private static int ResolveExtension(ReadOnlySpan<byte> span, int offset, int type)
    {
        if (type == ExtensionType)
            throw new FontFormatException("A GSUB extension subtable points to another extension.");

        long target = offset + (long)BigEndian.UInt32(span, offset + 4);

        // Clamped rather than cast, so an offset past the table fails the read that follows instead of wrapping.
        return (int)Math.Min(target, int.MaxValue);
    }
}
