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
    private Lazy<OpenTypeFont> _font;
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

        _font = LoadOnce(loaded);
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

    /// <summary>
    /// A typeface name the face was registered under besides its own, which a document can name it by; null when it
    /// goes by its own names only.
    /// </summary>
    public string? Alias { get; private init; }

    /// <summary>True when the face's outlines can be embedded in a PDF: TrueType or CFF.</summary>
    public bool IsEmbeddable => Outlines is OutlineFormat.TrueType or OutlineFormat.Cff;

    /// <summary>True once the whole font has been loaded, which describing and matching it never requires.</summary>
    public bool IsLoaded => Volatile.Read(ref _font).IsValueCreated;

    /// <summary>Describes a face already loaded, as a registered font is, keeping that very instance.</summary>
    public static FontFaceInfo FromFont(FontFileSource source, OpenTypeFont font, bool registered, string? alias = null) =>
        new FontFaceInfo(source, font.FaceIndex, font.Names, font.Style, font.Outlines, registered, font) { Alias = alias };

    /// <summary>The font, loaded on first call and kept.</summary>
    /// <exception cref="FontFormatException">The file is not a font this library can read.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public OpenTypeFont Load()
    {
        Lazy<OpenTypeFont> font = Volatile.Read(ref _font);

        try
        {
            return font.Value;
        }
        catch
        {
            // The lazy value keeps its exception, and the face would stay broken for as long as the process runs over
            // what may have been a file locked for a moment: the failed load is replaced by a fresh one, tried the next
            // time the face is used.
            Interlocked.CompareExchange(ref _font, LoadOnce(loaded: null), font);
            throw;
        }
    }

    /// <summary>
    /// True when the face has a glyph for the code point. A face whose character map cannot be read covers
    /// nothing, so one broken installed font cannot break fallback for every document.
    /// </summary>
    public bool Covers(int codepoint)
    {
        Lazy<OpenTypeFont> font = Volatile.Read(ref _font);

        if (font.IsValueCreated)
            return font.Value.HasGlyph(codepoint);

        try
        {
            return _coverage.Value?.GetGlyph(codepoint) is > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read now covers nothing now, but is asked again next time: it may only have been
            // locked for a moment, and the coverage kept would leave the face out of fallback for good.
            return false;
        }
    }

    public override string ToString() => $"{Names.FullName} ({FilePath ?? "registered"}#{FaceIndex})";

    /// <summary>
    /// Loading once per face matters: two threads that both pick a large CJK font must not both read it.
    /// </summary>
    private Lazy<OpenTypeFont> LoadOnce(OpenTypeFont? loaded) =>
        new Lazy<OpenTypeFont>(
            () => loaded ?? OpenTypeFont.Load(_source.Data, FaceIndex), LazyThreadSafetyMode.ExecutionAndPublication);

    private CharacterMap? ReadCoverage()
    {
        try
        {
            // Once the file is in memory — always so for a font registered from bytes, which has no path — parsing
            // the face costs less than reading its character map from disk again.
            return _source.IsLoaded
                ? Load().CharacterMap
                : FontFileScanner.ReadCharacterMap(_source.Path!, FaceIndex);
        }
        catch (FontFormatException)
        {
            // Kept: the same bytes will not read any better next time.
            return null;
        }
    }
}
