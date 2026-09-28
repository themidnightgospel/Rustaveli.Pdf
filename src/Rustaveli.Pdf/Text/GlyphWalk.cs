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

    internal GlyphWalk(
        TypeShaper shaper,
        OpenTypeFont primary,
        FontRequest request,
        ReadOnlySpan<char> text,
        float pointSize,
        float wordSpacing = 0f,
        TypeFeatures? features = null,
        TypefaceFallbacks? fallbacks = null)
    {
        _shaper = shaper;
        _primary = primary;
        _request = request;
        _text = text;
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

        if (_buffer is not null)
        {
            if (_bufferIndex < _buffer.Count)
            {
                EmitShaped();
                return true;
            }

            Release();
        }

        if (_next >= _text.Length)
            return false;

        int start = _next;
        (int codepoint, int length) = Read(start);
        OpenTypeFont face = _shaper.FaceFor(_primary, _request, _fallbacks, codepoint);

        if (start >= _plainEnd && (face.Substitutions is not null || _shaper.Complex is not null) && Shape(face, start, length))
        {
            EmitShaped();
            return true;
        }

        ushort glyph = face.GetGlyphId(codepoint);
        Emit(face, glyph, codepoint, start, length, text: null);
        _next = start + length;
        return true;
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

            if (!ReferenceEquals(_shaper.FaceFor(_primary, _request, _fallbacks, codepoint), face))
                break;

            end += length;
        }

        ReadOnlySpan<char> run = _text.Slice(start, end - start);

        if (_shaper.Complex is IComplexShaper complex && complex.Handles(run))
            return ShapeComplex(complex, face, start, run);

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
    /// are.
    /// </summary>
    private bool ShapeComplex(IComplexShaper complex, OpenTypeFont face, int start, ReadOnlySpan<char> run)
    {
        ShapingScratch scratch = TypeShaper.RentScratch();
        GlyphBuffer buffer = scratch.Buffer;
        List<ComplexGlyph> placements = scratch.Placements;

        placements.Clear();
        complex.Shape(face, run, _pointSize, _features, placements);
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
    /// The next glyph of the shaped run. The first glyph of a cluster stands for its characters; any further glyphs
    /// of the cluster, from a substitution that made several of one, stand for none, so text read back is not doubled.
    /// </summary>
    private void EmitShaped()
    {
        GlyphBuffer buffer = _buffer!;
        int index = _bufferIndex++;
        int cluster = buffer.Clusters[index];
        bool opens = index == 0 || buffer.Clusters[index - 1] != cluster;
        int start = _runStart + cluster;
        int length = opens ? buffer.GetClusterEnd(index, _runLength) - cluster : 0;
        (int codepoint, int single) = Read(start);

        string? text = !opens ? string.Empty
            : length > single ? _text.Slice(start, length).ToString()
            : null;

        List<ComplexGlyph> placements = _scratch!.Placements;
        ComplexGlyph? placed = placements.Count > 0 ? placements[index] : null;

        Emit(_bufferFace!, buffer.Glyphs[index], codepoint, start, length, text, placed);
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
