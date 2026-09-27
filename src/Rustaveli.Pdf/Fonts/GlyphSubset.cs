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
/// Each glyph remembers the first text it was used for, which is what a ToUnicode CMap needs to make the text
/// searchable and copyable.
/// </para>
/// <para>
/// Safe to use from several threads, though a document is normally written by one. Every glyph drawn passes through
/// here, and almost every one is a glyph already recorded with its text, so that case takes no lock and allocates
/// nothing; only a glyph's first use, or one that gives it text at last, locks — this instance only, so documents
/// never contend with one another.
/// </para>
/// </remarks>
internal sealed class GlyphSubset
{
    private const int NoCodepoint = -1;

    private readonly object _sync = new object();
    private readonly List<ushort> _glyphs = [0];

    // By original glyph id, the subset glyph id handed out for it; 0 until then, since .notdef is never numbered.
    private readonly ushort[] _numbers;

    // By subset glyph id, the text the glyph reads as. Replaced, never resized in place, so a reader without the lock
    // always sees a whole array at least as new as the number it read.
    private string?[] _texts = new string?[16];

    public GlyphSubset(OpenTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Font = font;
        _numbers = new ushort[font.GlyphCount];
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
    public ushort Add(ushort glyph) => Add(glyph, NoCodepoint, null);

    /// <summary>Records a use of <paramref name="glyph"/> to show <paramref name="codepoint"/>.</summary>
    /// <remarks>A value that is no Unicode scalar value reads back as the replacement character, U+FFFD.</remarks>
    public ushort Add(ushort glyph, int codepoint) => Add(glyph, codepoint, null);

    /// <summary>
    /// Records a use of <paramref name="glyph"/> to show <paramref name="text"/> — one character, or all of a
    /// ligature's — and returns its subset glyph id.
    /// </summary>
    public ushort Add(ushort glyph, string? text) => Add(glyph, NoCodepoint, text);

    /// <summary>
    /// Records a use of <paramref name="glyph"/> to show <paramref name="text"/>, or <paramref name="codepoint"/> when
    /// there is no text, and returns its subset glyph id. The first text recorded for a glyph is kept. Empty text is a
    /// glyph standing for none of its own, such as the accent of a letter set as two glyphs: it reads back as
    /// nothing, rather than as whatever the font's character map says, until a use that shows text replaces it.
    /// </summary>
    /// <remarks>
    /// .notdef stands in for every character the font lacks, so it is never associated with one: a ToUnicode entry
    /// for it would turn every missing character into whichever happened to be missing first.
    /// </remarks>
    public ushort Add(ushort glyph, int codepoint, string? text)
    {
        if (glyph >= Font.GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph), glyph, $"The font has {Font.GlyphCount} glyphs.");

        if (glyph == 0)
            return 0;

        bool offers = text is not null || codepoint != NoCodepoint;
        ushort known = Volatile.Read(ref _numbers[glyph]);

        if (known != 0)
        {
            string? recorded = Volatile.Read(ref _texts)[known];

            // Nothing to change: the glyph reads as text already, or this use offers none better than it has.
            if (!string.IsNullOrEmpty(recorded) || !offers || (text is { Length: 0 } && recorded is not null))
                return known;
        }

        string? offered = text ?? (codepoint == NoCodepoint ? null : TextOf(codepoint));

        lock (_sync)
        {
            ushort number = _numbers[glyph];

            if (number != 0)
            {
                _texts[number] = Keep(_texts[number], offered);
                return number;
            }

            // At most one entry per glyph of a font whose glyph count is 16-bit, so the number always fits.
            number = (ushort)_glyphs.Count;
            _glyphs.Add(glyph);

            if (number >= _texts.Length)
            {
                string?[] grown = new string?[_texts.Length * 2];
                _texts.CopyTo(grown, 0);
                Volatile.Write(ref _texts, grown);
            }

            _texts[number] = offered;

            // Published last, so a reader that finds the number finds its text slot too.
            Volatile.Write(ref _numbers[glyph], number);
            return number;
        }
    }

    /// <summary>The text a subset glyph was first used for; false for .notdef and glyphs added without.</summary>
    public bool TryGetText(ushort subsetGlyphId, out string text)
    {
        lock (_sync)
        {
            text = (subsetGlyphId < _glyphs.Count ? _texts[subsetGlyphId] : null)!;
            return text is not null;
        }
    }

    /// <summary>
    /// The text a glyph reads back as once it has also been used to show <paramref name="offered"/>: the first text
    /// shown, where there is one, else the first recorded at all.
    /// </summary>
    internal static string? Keep(string? recorded, string? offered) =>
        string.IsNullOrEmpty(recorded) && !string.IsNullOrEmpty(offered) ? offered : recorded ?? offered;

    /// <summary>A code point as text; the replacement character for a value that is no Unicode scalar value.</summary>
    internal static string TextOf(int codepoint) =>
        char.ConvertFromUtf32(codepoint is >= 0 and <= 0x10FFFF and (< 0xD800 or > 0xDFFF) ? codepoint : 0xFFFD);

    /// <summary>Builds the subset font, keeping every subset glyph id handed out so far.</summary>
    public TrueTypeSubset Build() => TrueTypeSubsetter.SubsetInOrder(Font, OriginalGlyphIds);
}
