using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Walks text glyph by glyph, choosing a face for each character and kerning neighbours set in the same face.
/// </summary>
/// <remarks>
/// Measuring, fitting and drawing all walk text through here, so they cannot disagree about which face sets a
/// character or how far it moves the pen — the failure that would otherwise put a glyph in one place and reserve
/// its space in another. It allocates nothing: text is measured far more often than it is drawn.
/// </remarks>
internal ref struct GlyphWalk
{
    private readonly TypeShaper _shaper;
    private readonly OpenTypeFont _primary;
    private readonly FontRequest _request;
    private readonly ReadOnlySpan<char> _text;
    private readonly float _pointSize;
    private readonly float _wordSpacing;
    private int _next;
    private OpenTypeFont? _previousFace;
    private ushort _previousGlyph;

    internal GlyphWalk(
        TypeShaper shaper, OpenTypeFont primary, FontRequest request, ReadOnlySpan<char> text, float pointSize, float wordSpacing = 0f)
    {
        _shaper = shaper;
        _primary = primary;
        _request = request;
        _text = text;
        _pointSize = pointSize;
        _wordSpacing = wordSpacing;
        _next = 0;
        _previousFace = null;
        _previousGlyph = 0;
        Current = default;
    }

    public ShapedGlyph Current { get; private set; }

    public readonly GlyphWalk GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_next >= _text.Length)
            return false;

        int start = _next;
        char character = _text[start];
        bool pair = char.IsHighSurrogate(character) && start + 1 < _text.Length && char.IsLowSurrogate(_text[start + 1]);
        int codepoint = pair ? char.ConvertToUtf32(character, _text[start + 1]) : character;
        int length = pair ? 2 : 1;

        OpenTypeFont face = _shaper.FaceFor(_primary, _request, codepoint);
        ushort glyph = face.GetGlyphId(codepoint);
        float advance = face.GetAdvance(glyph, _pointSize);
        float kerning = ReferenceEquals(face, _previousFace) ? face.GetKerning(_previousGlyph, glyph, _pointSize) : 0f;

        // Word spacing widens the spaces between words, the no-break space among them; it is carried by the space
        // itself, so a space measured on its own is as wide as it will be set.
        float extra = codepoint is ' ' or ' ' ? _wordSpacing : 0f;

        Current = new ShapedGlyph(face, glyph, codepoint, start, length, advance, kerning, extra);
        _previousFace = face;
        _previousGlyph = glyph;
        _next = start + length;
        return true;
    }
}
