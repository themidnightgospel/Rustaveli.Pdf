using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Walks text glyph by glyph, choosing a face for each character, applying the face's substitutions — ligatures,
/// contextual and stylistic forms — and kerning neighbours set in the same face.
/// </summary>
/// <remarks>
/// <para>
/// Measuring, fitting and drawing all walk text through here, so they cannot disagree about which face sets a
/// character, which glyph it becomes or how far it moves the pen — the failure that would otherwise put a glyph in
/// one place and reserve its space in another.
/// </para>
/// <para>
/// Substitution works on a run of characters set in one face, since a ligature joins neighbours: the run is shaped
/// whole into a buffer, then handed out a glyph at a time, each with the characters it stands for. A face with no
/// substitution that applies is walked a character at a time and allocates nothing, and neither, once warm, does a
/// face with them: the buffer is kept per thread between walks.
/// </para>
/// </remarks>
internal ref struct GlyphWalk
{
    private const int NoBreakSpace = 0x00A0;

    private readonly TypeShaper _shaper;
    private readonly OpenTypeFont _primary;
    private readonly FontRequest _request;
    private readonly ReadOnlySpan<char> _text;
    private readonly ReadOnlySpan<char> _typed;
    private readonly bool _rightToLeft;
    private readonly float _pointSize;
    private readonly float _wordSpacing;
    private readonly TypeFeatures _features;
    private readonly TypefaceFallbacks _fallbacks;
    private int _next;
    private int _plainEnd;
    private OpenTypeFont? _previousFace;
    private ushort _previousGlyph;
    private ShapingScratch? _scratch;
    private GlyphBuffer? _buffer;
    private OpenTypeFont? _bufferFace;
    private int _bufferIndex;
    private int _runStart;
    private int _runLength;
    private List<ShapedGlyph>? _replay;
    private int _replayed;

    /// <param name="shaper">Chooses faces and substitutions.</param>
    /// <param name="primary">The style's own face.</param>
    /// <param name="request">The face asked for, which fallbacks are matched against.</param>
    /// <param name="text">The text to set, with any character that has a mirror image taken as it when it reads right to left.</param>
    /// <param name="pointSize">The size it is set at.</param>
    /// <param name="wordSpacing">Space added to each word space.</param>
    /// <param name="features">The features the style turns on or off.</param>
    /// <param name="fallbacks">The style's own fallback typefaces.</param>
    /// <param name="typed">
    /// For text that reads right to left, the text as typed, before mirroring: a complex shaper mirrors characters
    /// itself, and mirroring them twice would turn a bracket back to face the wrong way. Empty for text read left to
    /// right.
    /// </param>
    internal GlyphWalk(
        TypeShaper shaper,
        OpenTypeFont primary,
        FontRequest request,
        ReadOnlySpan<char> text,
        float pointSize,
        float wordSpacing = 0f,
        TypeFeatures? features = null,
        TypefaceFallbacks? fallbacks = null,
        ReadOnlySpan<char> typed = default)
    {
        _shaper = shaper;
        _primary = primary;
        _request = request;
        _text = text;
        _rightToLeft = !typed.IsEmpty;
        _typed = _rightToLeft ? typed : text;
        _pointSize = pointSize;
        _wordSpacing = wordSpacing;
        _features = features ?? TypeFeatures.None;
        _fallbacks = fallbacks ?? TypefaceFallbacks.None;
        _next = 0;
        _plainEnd = 0;
        _previousFace = null;
        _previousGlyph = 0;
        _scratch = null;
        _buffer = null;
        _bufferFace = null;
        _bufferIndex = 0;
        _runStart = 0;
        _runLength = 0;
        Current = default;
    }

    public ShapedGlyph Current { get; private set; }

    public readonly GlyphWalk GetEnumerator() => this;

    /// <summary>A walk handing out glyphs already shaped and put in order, as right-to-left text is.</summary>
    internal static GlyphWalk Replaying(List<ShapedGlyph> glyphs)
    {
        GlyphWalk walk = default;
        walk._replay = glyphs;
        return walk;
    }

    public bool MoveNext()
    {
        if (_replay is not null)
        {
            if (_replayed == _replay.Count)
                return false;

            Current = _replay[_replayed++];
            return true;
        }

        while (true)
        {
            if (_buffer is not null)
            {
                while (_bufferIndex < _buffer.Count)
                {
                    if (EmitShaped())
                        return true;
                }

                Release();
            }

            if (_next >= _text.Length)
                return false;

            int start = _next;
            (int codepoint, int length) = Read(start);
            OpenTypeFont face = FaceFor(codepoint, _previousFace);

            if (start >= _plainEnd && (face.Substitutions is not null || _shaper.Complex is not null) && Shape(face, start, length))
                continue;

            ushort glyph = face.GetGlyphId(codepoint);
            int end = PastNothing(face, start + length);
            _next = end;

            // One with nothing before it to go with is left out.
            if (IsSetAsNothing(face, codepoint))
                continue;

            if (codepoint == InvisibleCharacters.Tab)
                (codepoint, glyph) = (' ', face.GetGlyphId(' '));

            Emit(face, glyph, codepoint, start, end - start, end - start > length ? _text.Slice(start, end - start).ToString() : null);
            return true;
        }
    }

    /// <summary>
    /// The face that sets <paramref name="codepoint"/>, after a character set in <paramref name="previous"/>: an
    /// invisible character goes with the one before it, and any other is set in the first face that has it.
    /// </summary>
    private readonly OpenTypeFont FaceFor(int codepoint, OpenTypeFont? previous) =>
        InvisibleCharacters.Contains(codepoint) || codepoint == InvisibleCharacters.Tab
            ? previous ?? _primary
            : _shaper.FaceFor(_primary, _request, _fallbacks, codepoint);

    /// <summary>
    /// Whether a character is drawn as nothing in <paramref name="face"/>: an invisible one it has no glyph for, or a
    /// soft hyphen, which the face's glyph would show mid-line.
    /// </summary>
    private static bool IsSetAsNothing(OpenTypeFont face, int codepoint) =>
        InvisibleCharacters.Contains(codepoint) && (codepoint == InvisibleCharacters.SoftHyphen || !face.HasGlyph(codepoint));

    /// <summary>
    /// Where the characters from <paramref name="index"/> that <paramref name="face"/> sets as nothing end, so that
    /// they go with the glyph before them.
    /// </summary>
    private readonly int PastNothing(OpenTypeFont face, int index)
    {
        while (index < _text.Length)
        {
            (int codepoint, int length) = Read(index);

            if (!IsSetAsNothing(face, codepoint))
                break;

            index += length;
        }

        return index;
    }

    /// <summary>Hands the shaping buffer back if the walk was left before its end.</summary>
    public void Dispose() => Release();

    /// <summary>
    /// Shapes the run of characters <paramref name="face"/> sets from <paramref name="start"/> into the buffer;
    /// false, and the run remembered as plain, when none of its substitutions applies to it.
    /// </summary>
    private bool Shape(OpenTypeFont face, int start, int firstLength)
    {
        int end = start + firstLength;

        while (end < _text.Length)
        {
            (int codepoint, int length) = Read(end);

            if (!ReferenceEquals(FaceFor(codepoint, face), face))
                break;

            end += length;
        }

        ReadOnlySpan<char> run = _text.Slice(start, end - start);

        if (_shaper.Complex is IComplexShaper complex && complex.Handles(run))
            return ShapeComplex(complex, face, start, _typed.Slice(start, run.Length));

        IReadOnlyList<(int Index, int Value)> lookups = face.Substitutions is null
            ? []
            : _shaper.LookupsFor(face, ScriptDetection.Of(run), _features);

        if (lookups.Count == 0)
        {
            _plainEnd = end;
            return false;
        }

        ShapingScratch scratch = TypeShaper.RentScratch();
        GlyphBuffer buffer = scratch.Buffer;
        buffer.Load(face, run);
        scratch.Placements.Clear();

        try
        {
            GlyphSubstitutionTable table = face.Substitutions!;
            table.Apply(scratch.SessionFor(table), lookups);
        }
        catch (FontFormatException)
        {
            // A lookup the font got wrong: set the run as it would be without substitutions rather than lose it.
            buffer.Load(face, run);
        }

        _scratch = scratch;
        _buffer = buffer;
        _bufferFace = face;
        _bufferIndex = 0;
        _runStart = start;
        _runLength = run.Length;
        _next = end;
        return true;
    }

    /// <summary>
    /// Hands a run to the complex shaper, which places its glyphs itself — advances with any kerning, and offsets for
    /// marks — and loads what it sets into the buffer, so they are handed out cluster by cluster as substituted glyphs
    /// are. The run is the text as typed, which the shaper mirrors itself when it reads right to left; the glyphs
    /// still read as the text walked, as the core's do.
    /// </summary>
    private bool ShapeComplex(IComplexShaper complex, OpenTypeFont face, int start, ReadOnlySpan<char> run)
    {
        ShapingScratch scratch = TypeShaper.RentScratch();
        GlyphBuffer buffer = scratch.Buffer;
        List<ComplexGlyph> placements = scratch.Placements;

        placements.Clear();
        complex.Shape(face, run, _pointSize, _features, _rightToLeft, placements);
        buffer.Load(face, ReadOnlySpan<char>.Empty);

        foreach (ComplexGlyph placed in placements)
            buffer.Add(placed.Glyph, placed.Cluster);

        _scratch = scratch;
        _buffer = buffer;
        _bufferFace = face;
        _bufferIndex = 0;
        _runStart = start;
        _runLength = run.Length;
        _next = start + run.Length;
        return true;
    }

    /// <summary>
    /// The next glyph of the shaped run. The first glyph of a cluster stands for its characters, and for any after it
    /// that are set as nothing; any further glyphs of the cluster, from a substitution that made several of one, stand
    /// for none, so text read back is not doubled. False, and nothing handed out, for a glyph set as nothing.
    /// </summary>
    private bool EmitShaped()
    {
        GlyphBuffer buffer = _buffer!;
        int index = _bufferIndex++;

        // Invisible characters the face has no glyph for went with the glyph before them, or, with none before them,
        // are left out.
        if (IsNothing(buffer, index))
            return false;

        int cluster = buffer.Clusters[index];
        bool opens = index == 0 || buffer.Clusters[index - 1] != cluster;
        int start = _runStart + cluster;
        int length = opens ? ClusterEnd(buffer, index) - cluster : 0;
        (int codepoint, int single) = Read(start);
        ushort glyph = buffer.Glyphs[index];

        string? text = !opens ? string.Empty
            : length > single ? _text.Slice(start, length).ToString()
            : null;

        List<ComplexGlyph> placements = _scratch!.Placements;
        ComplexGlyph? placed = placements.Count > 0 ? placements[index] : null;

        if (opens && codepoint == InvisibleCharacters.Tab)
            (codepoint, glyph, placed) = (' ', _bufferFace!.GetGlyphId(' '), null);

        Emit(_bufferFace!, glyph, codepoint, start, length, text, placed);
        return true;
    }

    /// <summary>
    /// Whether the glyph at <paramref name="index"/> stands for nothing drawn: the face's missing glyph for a cluster
    /// of invisible characters, or a soft hyphen's.
    /// </summary>
    private readonly bool IsNothing(GlyphBuffer buffer, int index)
    {
        int start = _runStart + buffer.Clusters[index];

        if (buffer.Glyphs[index] != 0)
            return _text[start] == InvisibleCharacters.SoftHyphen;

        int end = _runStart + buffer.GetClusterEnd(index, _runLength);

        for (int at = start; at < end;)
        {
            (int codepoint, int length) = Read(at);

            if (!InvisibleCharacters.Contains(codepoint))
                return false;

            at += length;
        }

        return true;
    }

    /// <summary>
    /// Where the characters the cluster at <paramref name="index"/> stands for end, counting the clusters after it
    /// that stand for nothing drawn, in code units from the start of the run.
    /// </summary>
    private readonly int ClusterEnd(GlyphBuffer buffer, int index)
    {
        ReadOnlySpan<int> clusters = buffer.Clusters;
        int end = buffer.GetClusterEnd(index, _runLength);
        int next = index + 1;

        while (next < clusters.Length && clusters[next] == clusters[index])
            next++;

        for (; next < clusters.Length && IsNothing(buffer, next); next++)
            end = buffer.GetClusterEnd(next, _runLength);

        return end;
    }

    private void Emit(OpenTypeFont face, ushort glyph, int codepoint, int start, int length, string? text, ComplexGlyph? placed = null)
    {
        // A glyph a complex shaper placed moves the pen as it said, kerning included, and is drawn where it said.
        float advance = placed?.Advance ?? face.GetAdvance(glyph, _pointSize);
        float kerning = placed is null && ReferenceEquals(face, _previousFace) ? face.GetKerning(_previousGlyph, glyph, _pointSize) : 0f;

        // Word spacing widens the spaces between words, the no-break space among them; it is carried by the space
        // itself, so a space measured on its own is as wide as it will be set.
        float extra = text is null && codepoint is ' ' or NoBreakSpace ? _wordSpacing : 0f;

        Current = new ShapedGlyph(
            face, glyph, codepoint, start, length, advance, kerning, extra, text, placed?.XOffset ?? 0f, placed?.YOffset ?? 0f);
        _previousFace = face;
        _previousGlyph = glyph;
    }

    private readonly (int Codepoint, int Length) Read(int index)
    {
        char character = _text[index];

        return char.IsHighSurrogate(character) && index + 1 < _text.Length && char.IsLowSurrogate(_text[index + 1])
            ? (char.ConvertToUtf32(character, _text[index + 1]), 2)
            : (character, 1);
    }

    private void Release()
    {
        if (_buffer is null)
            return;

        TypeShaper.ReturnScratch(_scratch!);
        _scratch = null;
        _buffer = null;
        _bufferFace = null;
    }
}
