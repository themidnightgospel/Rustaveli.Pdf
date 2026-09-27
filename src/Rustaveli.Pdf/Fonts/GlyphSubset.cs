namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Collects the glyphs a document uses from one font, numbering each on first use, so content streams can be written
/// before the subset is built.
/// </summary>
/// <remarks>
/// <para>
/// Pages are written as they are laid out, long before the last glyph of the document is known. Numbering glyphs
/// in order of first use gives each a subset glyph id at once, which the content stream shows as its CID; building
/// the subset at the end keeps those ids and appends only the component glyphs of composites after them.
/// </para>
/// <para>
/// Each glyph remembers the first character it was used for, which is what a ToUnicode CMap needs to make the text
/// searchable and copyable.
/// </para>
/// <para>
/// Safe to use from several threads, though a document is normally written by one; the lock is this instance's,
/// so documents never contend with one another.
/// </para>
/// </remarks>
internal sealed class GlyphSubset
{
    private const int NoCodepoint = -1;

    private readonly object _sync = new object();
    private readonly List<ushort> _glyphs = [0];
    private readonly List<int> _codepoints = [NoCodepoint];
    private readonly Dictionary<ushort, ushort> _numbers = new Dictionary<ushort, ushort> { [0] = 0 };

    public GlyphSubset(OpenTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Font = font;
    }

    public OpenTypeFont Font { get; }

    /// <summary>The number of glyphs used so far, .notdef included.</summary>
    public int Count
    {
        get
        {
            lock (_sync)
                return _glyphs.Count;
        }
    }

    /// <summary>For each subset glyph id so far, the original glyph id; entry 0 is .notdef.</summary>
    public IReadOnlyList<ushort> OriginalGlyphIds
    {
        get
        {
            lock (_sync)
                return _glyphs.ToArray();
        }
    }

    /// <summary>Records a use of <paramref name="glyph"/> and returns its subset glyph id.</summary>
    public ushort Add(ushort glyph) => Add(glyph, NoCodepoint);

    /// <summary>
    /// Records a use of <paramref name="glyph"/> to show <paramref name="codepoint"/>, and returns its subset glyph
    /// id. The first character recorded for a glyph is kept.
    /// </summary>
    /// <remarks>
    /// .notdef stands in for every character the font lacks, so it is never associated with one: a ToUnicode entry
    /// for it would turn every missing character into whichever happened to be missing first.
    /// </remarks>
    public ushort Add(ushort glyph, int codepoint)
    {
        if (glyph >= Font.GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph), glyph, $"The font has {Font.GlyphCount} glyphs.");

        if (glyph == 0)
            return 0;

        lock (_sync)
        {
            if (_numbers.TryGetValue(glyph, out ushort number))
            {
                if (_codepoints[number] == NoCodepoint)
                    _codepoints[number] = codepoint;

                return number;
            }

            // At most one entry per glyph of a font whose glyph count is 16-bit, so the number always fits.
            number = (ushort)_glyphs.Count;
            _glyphs.Add(glyph);
            _codepoints.Add(codepoint);
            _numbers.Add(glyph, number);
            return number;
        }
    }

    /// <summary>The character a subset glyph was first used for; false for .notdef and glyphs added without.</summary>
    public bool TryGetCodepoint(ushort subsetGlyphId, out int codepoint)
    {
        lock (_sync)
        {
            codepoint = subsetGlyphId < _codepoints.Count ? _codepoints[subsetGlyphId] : NoCodepoint;
            return codepoint != NoCodepoint;
        }
    }

    /// <summary>Builds the subset font, keeping every subset glyph id handed out so far.</summary>
    public TrueTypeSubset Build() => TrueTypeSubsetter.SubsetInOrder(Font, OriginalGlyphIds);
}
