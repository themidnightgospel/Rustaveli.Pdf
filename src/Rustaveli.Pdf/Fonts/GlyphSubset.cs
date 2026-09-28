namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Collects the glyphs a document uses from one font, numbering each on first use, so content streams can be written
/// before the subset is built; and gives each distinct use of a glyph the code a content stream shows it by.
/// </summary>
/// <remarks>
/// <para>
/// Pages are written as they are laid out, long before the last glyph of the document is known. Numbering glyphs
/// in order of first use gives each a subset glyph id at once; building the subset at the end keeps those ids and
/// appends only the component glyphs of composites after them.
/// </para>
/// <para>
/// A glyph's code is its subset glyph id, and the text it reads back as — what a ToUnicode CMap records — is the text
/// of its first use. A glyph can stand for different text, though: two characters a font draws alike, a letter's
/// shared skeleton in Arabic, a mark that stands for nothing after its letter and for itself alone. A use with other
/// text than the first gets a code of its own, counted from <see cref="FirstSharedCode"/>, which the PDF maps back to
/// the glyph; so every code reads back as what it shows. A document without such uses keeps the identity mapping.
/// </para>
/// <para>
/// Safe to use from several threads, though a document is normally written by one. Every glyph drawn passes through
/// here, and almost every one is a glyph already recorded with the same text, so that case takes no lock and
/// allocates nothing; only a glyph's first use, or one with other text, locks — this instance only, so documents
/// never contend with one another.
/// </para>
/// </remarks>
internal sealed class GlyphSubset
{
    /// <summary>
    /// The first code given to a glyph shown with other text than its first; codes below it are subset glyph ids.
    /// </summary>
    public const ushort FirstSharedCode = 0x8000;

    private const int NoCodepoint = -1;

    private readonly object _sync = new object();
    private readonly List<ushort> _glyphs = [0];

    // By original glyph id, the subset glyph id handed out for it; 0 until then, since .notdef is never numbered.
    private readonly ushort[] _numbers;

    // The further codes a glyph was given, by subset glyph id and text, and each one's glyph and text in code order.
    private readonly Dictionary<(ushort Glyph, string Text), ushort> _shared = [];
    private readonly List<(ushort Glyph, string Text)> _sharedCodes = [];

    // By subset glyph id, the text the glyph's own code reads as. Replaced, never resized in place, so a reader without
    // the lock always sees a whole array at least as new as the number it read.
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

    /// <summary>For each code from <see cref="FirstSharedCode"/> on, the subset glyph id it shows and what it reads as.</summary>
    public IReadOnlyList<(ushort Glyph, string Text)> SharedCodes
    {
        get
        {
            lock (_sync)
                return _sharedCodes.ToArray();
        }
    }

    /// <summary>Records a use of <paramref name="glyph"/> and returns its code.</summary>
    public ushort Add(ushort glyph) => Add(glyph, NoCodepoint, null);

    /// <summary>Records a use of <paramref name="glyph"/> to show <paramref name="codepoint"/>.</summary>
    /// <remarks>A value that is no Unicode scalar value reads back as the replacement character, U+FFFD.</remarks>
    public ushort Add(ushort glyph, int codepoint) => Add(glyph, codepoint, null);

    /// <summary>
    /// Records a use of <paramref name="glyph"/> to show <paramref name="text"/> — one character, or all of a
    /// ligature's — and returns its code.
    /// </summary>
    public ushort Add(ushort glyph, string? text) => Add(glyph, NoCodepoint, text);

    /// <summary>
    /// Records a use of <paramref name="glyph"/> to show <paramref name="text"/>, or <paramref name="codepoint"/> when
    /// there is no text, and returns the code that shows it so: the glyph's own code the first time, and whenever the
    /// text is the same; another when it differs. Empty text is a glyph standing for no characters of its own, such
    /// as the accent of a letter set as two glyphs, which reads back as nothing.
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

        ushort known = Volatile.Read(ref _numbers[glyph]);

        // A use that offers no text, or the text the glyph's own code already reads as.
        if (known != 0 && ShowsTheSame(Volatile.Read(ref _texts)[known], codepoint, text))
            return known;

        string? offered = text ?? (codepoint == NoCodepoint ? null : TextOf(codepoint));

        lock (_sync)
        {
            ushort number = _numbers[glyph];

            if (number == 0)
                return Number(glyph, offered);

            string? recorded = _texts[number];

            // Nothing recorded yet, and the glyph's code has shown nothing that read otherwise.
            if (recorded is null || offered is null || recorded == offered)
            {
                _texts[number] ??= offered;
                return number;
            }

            return SharedCode(number, offered);
        }
    }

    /// <summary>What a code reads back as; false for .notdef and codes given no text.</summary>
    public bool TryGetText(ushort code, out string text)
    {
        lock (_sync)
        {
            if (code >= FirstSharedCode)
                text = code - FirstSharedCode < _sharedCodes.Count ? _sharedCodes[code - FirstSharedCode].Text : null!;
            else
                text = (code < _glyphs.Count ? _texts[code] : null)!;

            return text is not null;
        }
    }

    /// <summary>
    /// The text a glyph reads back as once it has also been used to show <paramref name="offered"/>: the first text
    /// shown, where there is one, else the first recorded at all. Used where a code cannot be told apart by what it
    /// shows, as in a font embedded whole.
    /// </summary>
    internal static string? Keep(string? recorded, string? offered) =>
        string.IsNullOrEmpty(recorded) && !string.IsNullOrEmpty(offered) ? offered : recorded ?? offered;

    /// <summary>A code point as text; the replacement character for a value that is no Unicode scalar value.</summary>
    internal static string TextOf(int codepoint) =>
        char.ConvertFromUtf32(codepoint is >= 0 and <= 0x10FFFF and (< 0xD800 or > 0xDFFF) ? codepoint : 0xFFFD);

    /// <summary>Builds the subset font, keeping every subset glyph id handed out so far.</summary>
    public TrueTypeSubset Build() => TrueTypeSubsetter.SubsetInOrder(Font, OriginalGlyphIds);

    /// <summary>
    /// Whether a use shows what the glyph's own code reads as, or offers nothing to tell: checked without building
    /// the text, since almost every glyph drawn is one already recorded with the character it shows.
    /// </summary>
    private static bool ShowsTheSame(string? recorded, int codepoint, string? text)
    {
        if (text is not null)
            return recorded == text;

        if (codepoint == NoCodepoint)
            return true;

        return recorded is not null && recorded.Length == (codepoint > 0xFFFF ? 2 : 1) && char.ConvertToUtf32(recorded, 0) == codepoint;
    }

    private ushort Number(ushort glyph, string? offered)
    {
        // At most one entry per glyph of a font whose glyph count is 16-bit; a font this large is caught below.
        ushort number = (ushort)_glyphs.Count;

        if (number >= FirstSharedCode)
            throw new InvalidOperationException($"A document can use at most {FirstSharedCode - 1} glyphs of one font.");

        _glyphs.Add(glyph);

        if (number >= _texts.Length)
        {
            string?[] grown = new string?[_texts.Length * 2];
            _texts.CopyTo(grown, 0);
            Volatile.Write(ref _texts, grown);
        }

        _texts[number] = offered;

        // Published last, so a reader that finds the number finds its text too.
        Volatile.Write(ref _numbers[glyph], number);
        return number;
    }

    private ushort SharedCode(ushort number, string offered)
    {
        if (_shared.TryGetValue((number, offered), out ushort code))
            return code;

        if (_sharedCodes.Count > ushort.MaxValue - FirstSharedCode)
            throw new InvalidOperationException("A document can show glyphs of one font with at most 32768 other texts.");

        code = (ushort)(FirstSharedCode + _sharedCodes.Count);
        _sharedCodes.Add((number, offered));
        _shared.Add((number, offered), code);
        return code;
    }
}
