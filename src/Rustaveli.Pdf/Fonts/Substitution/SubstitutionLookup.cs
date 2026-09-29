namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// One GSUB lookup: subtables of one type, tried in order at each glyph, and flags naming the glyphs it passes over.
/// </summary>
internal sealed class SubstitutionLookup
{
    /// <summary>Reverse chaining contextual single substitution, the one type applied from the end back.</summary>
    public const int ReverseChainingType = 8;

    private readonly SubstitutionSubtable[] _subtables;
    private readonly GlyphDefinitionTable? _definitions;
    private readonly CoverageTable? _markFilteringSet;

    /// <summary>A lookup of subtables of one type, applied to the glyphs its flags let it see.</summary>
    /// <param name="type">The lookup type, an extension's being that of the subtables it wraps.</param>
    /// <param name="flags">The lookup flags.</param>
    /// <param name="subtables">The subtables, in the order they are tried.</param>
    /// <param name="definitions">The font's glyph classes; null when it has none, so nothing is passed over.</param>
    /// <param name="markFilteringSet">
    /// With <see cref="LookupFlags.UseMarkFilteringSet"/>, the marks the lookup sees; null for a set the font lacks.
    /// </param>
    public SubstitutionLookup(
        int type,
        LookupFlags flags,
        SubstitutionSubtable[] subtables,
        GlyphDefinitionTable? definitions,
        CoverageTable? markFilteringSet)
    {
        Type = type;
        Flags = flags;
        _subtables = subtables;
        _definitions = definitions;
        _markFilteringSet = markFilteringSet;
    }

    public int Type { get; }

    public LookupFlags Flags { get; }

    public IReadOnlyList<SubstitutionSubtable> Subtables => _subtables;

    /// <summary>True when the lookup passes over the glyph, as its flags and the font's glyph classes decide.</summary>
    public bool Skips(ushort glyph) => _definitions is not null && _definitions.Skips(glyph, Flags, _markFilteringSet);

    /// <summary>
    /// Applies the lookup across the whole buffer: at each glyph in turn, continuing after whatever a substitution
    /// consumed — or, for reverse chaining, at each glyph from the last back to the first.
    /// </summary>
    public void Apply(SubstitutionSession session)
    {
        if (Type == ReverseChainingType)
        {
            for (int position = session.Buffer.Count - 1; position >= 0; position--)
                TryApply(session, position, out _);

            return;
        }

        int index = 0;

        while (index < session.Buffer.Count)
        {
            session.ForgetEdits();
            index = TryApply(session, index, out int next) ? next : index + 1;
        }
    }

    /// <summary>
    /// Applies the first subtable that substitutes at <paramref name="position"/>; false when none does, or the
    /// lookup passes over the glyph there.
    /// </summary>
    /// <param name="session">The session applying the table.</param>
    /// <param name="position">The glyph to substitute at.</param>
    /// <param name="next">
    /// Where the lookup continues after a substitution: the glyph after those consumed. Without one, the position.
    /// </param>
    public bool TryApply(SubstitutionSession session, int position, out int next)
    {
        next = position;
        return !Skips(session.Buffer.Glyphs[position]) && TryApplyAt(session, position, out next);
    }

    /// <summary>
    /// Applies the first subtable that substitutes at <paramref name="position"/>, whatever the lookup's flags say of
    /// the glyph there: a contextual rule names the glyph its nested lookup applies to, and HarfBuzz applies it there.
    /// The flags still decide which glyphs the lookup matches past.
    /// </summary>
    /// <inheritdoc cref="TryApply" path="/param"/>
    public bool TryApplyAt(SubstitutionSession session, int position, out int next)
    {
        next = position;

        foreach (SubstitutionSubtable subtable in _subtables)
        {
            if (!session.Spend())
                return false;

            if (subtable.TryApply(session, this, position, out next))
                return true;
        }

        return false;
    }

    /// <summary>The next glyph after <paramref name="position"/> the lookup does not pass over; -1 when none.</summary>
    public int NextGlyph(SubstitutionSession session, int position)
    {
        ReadOnlySpan<ushort> glyphs = session.Buffer.Glyphs;

        for (int index = position + 1; index < glyphs.Length && session.Spend(); index++)
        {
            if (!Skips(glyphs[index]))
                return index;
        }

        return -1;
    }

    /// <summary>The nearest glyph before <paramref name="position"/> the lookup does not pass over, or -1.</summary>
    public int PreviousGlyph(SubstitutionSession session, int position)
    {
        ReadOnlySpan<ushort> glyphs = session.Buffer.Glyphs;

        for (int index = position - 1; index >= 0 && session.Spend(); index--)
        {
            if (!Skips(glyphs[index]))
                return index;
        }

        return -1;
    }
}
