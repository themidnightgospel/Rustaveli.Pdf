namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// One application of a GSUB table to a buffer: the buffer and a journal of its edits, the value of the feature
/// being applied, how deeply lookups are nested, and how much work may still be done.
/// </summary>
/// <remarks>
/// <para>
/// A font's lookups are a program the font supplies: contextual lookups call other lookups, and multiple
/// substitutions lengthen the buffer, so a crafted font could make either run away. The session caps the nesting
/// depth, the buffer's growth and the total work, each far beyond what real fonts need. A font that reaches a cap is
/// shaped as far as it got: a nested lookup too deep is not applied, a substitution that would grow the buffer too
/// far is not made, and once the work is spent every remaining lookup does nothing.
/// </para>
/// <para>
/// Work is counted per subtable tried at a position, per rule or ligature tried, and per glyph visited while
/// matching a sequence — everything whose count a font controls.
/// </para>
/// </remarks>
internal sealed class SubstitutionSession
{
    /// <summary>How deeply contextual lookups may call one another.</summary>
    public const int MaximumNesting = 16;

    private const long BaseWork = 1 << 16;
    private const long WorkPerGlyph = 1 << 14;
    private const long BaseLength = 256;
    private const long LengthPerGlyph = 16;

    private readonly List<(int Index, int Change)> _edits = [];
    private readonly List<int>?[] _positions = new List<int>?[MaximumNesting + 1];
    private int _maximumLength;
    private long _work;

    public SubstitutionSession(GlyphSubstitutionTable table, GlyphBuffer buffer)
        : this(table, buffer, WorkFor(buffer), LengthFor(buffer))
    {
    }

    /// <summary>A session with explicit limits, so tests can reach them with small inputs.</summary>
    public SubstitutionSession(GlyphSubstitutionTable table, GlyphBuffer buffer, long work, int maximumLength)
    {
        Table = table;
        Buffer = buffer;
        _work = work;
        _maximumLength = maximumLength;
    }

    public GlyphSubstitutionTable Table { get; private set; }

    public GlyphBuffer Buffer { get; private set; }

    /// <summary>
    /// Readies the session to apply <paramref name="table"/> to <paramref name="buffer"/> afresh, with the limits a
    /// new session would have, keeping the storage it has grown: text is shaped a word at a time, and a session per
    /// word would be most of what shaping allocates.
    /// </summary>
    public void Reset(GlyphSubstitutionTable table, GlyphBuffer buffer)
    {
        Table = table;
        Buffer = buffer;
        _work = WorkFor(buffer);
        _maximumLength = LengthFor(buffer);
        _edits.Clear();
        Depth = 0;
        FeatureValue = 1;
    }

    private static long WorkFor(GlyphBuffer buffer) => BaseWork + (WorkPerGlyph * buffer.Count);

    private static int LengthFor(GlyphBuffer buffer) => (int)Math.Min(int.MaxValue, BaseLength + (LengthPerGlyph * buffer.Count));

    /// <summary>The value of the feature whose lookup is being applied, which chooses among alternates.</summary>
    public int FeatureValue { get; set; } = 1;

    public int Depth { get; private set; }

    public int EditCount => _edits.Count;

    /// <summary>Accounts for one unit of work; false, and nothing more may be done, once the budget is spent.</summary>
    public bool Spend()
    {
        if (_work <= 0)
            return false;

        _work--;
        return true;
    }

    /// <summary>Enters a nested lookup; false when lookups are already nested as deeply as allowed.</summary>
    public bool TryEnter()
    {
        if (Depth >= MaximumNesting)
            return false;

        Depth++;
        return true;
    }

    public void Leave() => Depth--;

    /// <summary>
    /// The list every match at the current depth records its positions in, so matching allocates nothing. A match
    /// empties it before starting; a nested lookup, one level deeper, has a list of its own.
    /// </summary>
    public List<int> Positions() => _positions[Depth] ??= [];

    public bool IsGlyph(int glyph) => glyph >= 0 && glyph < Table.GlyphCount;

    /// <summary>Replaces the glyph; false, changing nothing, when the font has no such glyph.</summary>
    public bool TryReplace(int index, int glyph)
    {
        if (!IsGlyph(glyph))
            return false;

        Buffer.Replace(index, (ushort)glyph);
        return true;
    }

    /// <summary>
    /// Opens places after a glyph (see <see cref="GlyphBuffer.InsertAfter"/>); false, changing nothing, when the
    /// buffer would grow past its limit.
    /// </summary>
    public bool TryInsertAfter(int index, int count)
    {
        if (Buffer.Count + count > _maximumLength)
            return false;

        Buffer.InsertAfter(index, count);
        _edits.Add((index, count));
        return true;
    }

    public void Remove(int index)
    {
        Buffer.RemoveAt(index);
        _edits.Add((index, -1));
    }

    /// <summary>Discards the journal; done before each position of a lookup, when no match is in progress.</summary>
    public void ForgetEdits() => _edits.Clear();

    /// <summary>
    /// Brings positions matched before a nested lookup up to date with the edits it made, from journal entry
    /// <paramref name="firstEdit"/> on.
    /// </summary>
    /// <remarks>
    /// A removed glyph leaves the sequence, and glyphs inserted after one of its glyphs — by a multiple substitution
    /// — join it there, so a later lookup record's sequence index counts them, as the specification requires. Glyphs
    /// inserted after a glyph the match passed over stay outside it.
    /// </remarks>
    public void Replay(List<int> positions, int firstEdit)
    {
        for (int edit = firstEdit; edit < _edits.Count; edit++)
        {
            (int index, int change) = _edits[edit];
            int edited = -1;

            for (int entry = 0; entry < positions.Count; entry++)
            {
                if (positions[entry] > index)
                    positions[entry] += change;
                else if (positions[entry] == index)
                    edited = entry;
            }

            if (edited < 0)
                continue;

            if (change < 0)
            {
                positions.RemoveAt(edited);
                continue;
            }

            for (int inserted = 1; inserted <= change; inserted++)
                positions.Insert(edited + inserted, index + inserted);
        }
    }
}
