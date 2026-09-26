namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A font face known to a <see cref="FontCatalog"/>: what matching needs to know about it, and the means to load
/// it when it is chosen.
/// </summary>
/// <remarks>
/// A system face is described from its <c>name</c>, <c>OS/2</c> and <c>head</c> tables alone, read without loading
/// the file. Its character map is read the first time fallback asks whether it covers a character, and the whole
/// font only once it is actually used — so a machine with a thousand fonts costs a thousand small reads, not a
/// gigabyte of font data.
/// </remarks>
internal sealed class FontFaceInfo
{
    private readonly FontFileSource _source;
    private readonly Lazy<OpenTypeFont> _font;
    private readonly Lazy<CharacterMap?> _coverage;

    public FontFaceInfo(
        FontFileSource source, int faceIndex, FontNames names, FaceStyle style, OutlineFormat outlines, bool registered)
        : this(source, faceIndex, names, style, outlines, registered, loaded: null)
    {
    }

    private FontFaceInfo(
        FontFileSource source,
        int faceIndex,
        FontNames names,
        FaceStyle style,
        OutlineFormat outlines,
        bool registered,
        OpenTypeFont? loaded)
    {
        _source = source;
        FaceIndex = faceIndex;
        Names = names;
        Style = style;
        Outlines = outlines;
        IsRegistered = registered;

        // Loading once per face matters: two threads that both pick a large CJK font must not both read it.
        _font = new Lazy<OpenTypeFont>(
            () => loaded ?? OpenTypeFont.Load(_source.Data, FaceIndex), LazyThreadSafetyMode.ExecutionAndPublication);
        _coverage = new Lazy<CharacterMap?>(ReadCoverage, LazyThreadSafetyMode.PublicationOnly);

        if (loaded is not null)
            _ = _font.Value;
    }

    /// <summary>The file the face is in; null for a face registered from memory or a stream.</summary>
    public string? FilePath => _source.Path;

    /// <summary>The face's position in its file; 0 unless the file is a collection.</summary>
    public int FaceIndex { get; }

    public FontNames Names { get; }

    public FaceStyle Style { get; }

    public OutlineFormat Outlines { get; }

    /// <summary>True for a face the user registered, which takes precedence over installed fonts.</summary>
    public bool IsRegistered { get; }

    /// <summary>True when the face's outlines can be embedded in a PDF: TrueType or CFF.</summary>
    public bool IsEmbeddable => Outlines is OutlineFormat.TrueType or OutlineFormat.Cff;

    /// <summary>True once the whole font has been loaded, which describing and matching it never requires.</summary>
    public bool IsLoaded => _font.IsValueCreated;

    /// <summary>Describes a face already loaded, as a registered font is, keeping that very instance.</summary>
    public static FontFaceInfo FromFont(FontFileSource source, OpenTypeFont font, bool registered) =>
        new FontFaceInfo(source, font.FaceIndex, font.Names, font.Style, font.Outlines, registered, font);

    /// <summary>The font, loaded on first call and kept.</summary>
    /// <exception cref="FontFormatException">The file is not a font this library can read.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public OpenTypeFont Load() => _font.Value;

    /// <summary>
    /// True when the face has a glyph for the code point. A face whose character map cannot be read covers
    /// nothing, so one broken installed font cannot break fallback for every document.
    /// </summary>
    public bool Covers(int codepoint)
    {
        if (_font.IsValueCreated)
            return _font.Value.HasGlyph(codepoint);

        return _coverage.Value?.GetGlyph(codepoint) is > 0;
    }

    public override string ToString() => $"{Names.FullName} ({FilePath ?? "registered"}#{FaceIndex})";

    private CharacterMap? ReadCoverage()
    {
        try
        {
            return _source.IsLoaded || _source.Path is null
                ? Load().CharacterMap
                : FontFileScanner.ReadCharacterMap(_source.Path, FaceIndex);
        }
        catch (Exception exception) when (
            exception is FontFormatException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
