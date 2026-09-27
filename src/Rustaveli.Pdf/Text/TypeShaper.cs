using System.Collections.Concurrent;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Resolves type styles to faces and sets text as glyphs, for measuring and drawing alike.
/// </summary>
/// <remarks>
/// <para>
/// A style names a typeface. The face is the registered or installed one nearest the style's weight and slant; a
/// typeface nobody has falls back to a common substitute for its kind — a Helvetica request is answered by Arial,
/// Liberation Sans or another metric-compatible sans — as a desktop publishing application would substitute a
/// missing font rather than refuse to open the document.
/// </para>
/// <para>
/// A character the face lacks is set in the first face that has it: the configured fallback typefaces in order,
/// then the registered faces, then the installed ones. Faces found that way are remembered per style, so a run of
/// CJK text resolves its fallback once rather than once per character.
/// </para>
/// <para>Safe for concurrent use; one shaper serves every document set with its catalog.</para>
/// </remarks>
internal sealed class TypeShaper
{
    private static readonly string[] SansSubstitutes =
        ["Helvetica", "Arial", "Liberation Sans", "Nimbus Sans", "Arimo", "DejaVu Sans", "Noto Sans", "Segoe UI"];

    private static readonly string[] SerifSubstitutes =
        ["Times New Roman", "Times", "Liberation Serif", "Nimbus Roman", "Tinos", "DejaVu Serif", "Noto Serif"];

    private static readonly string[] MonospaceSubstitutes =
        ["Courier New", "Courier", "Liberation Mono", "Nimbus Mono PS", "Cousine", "DejaVu Sans Mono", "Noto Sans Mono"];

    private readonly FontCatalog _catalog;
    private readonly IReadOnlyList<string> _fallbackTypefaces;
    private readonly ConcurrentDictionary<FontRequest, OpenTypeFont> _faces = new(FontCatalog.FamilyIgnoringCase.Instance);
    private readonly ConcurrentDictionary<(OpenTypeFont Primary, int Codepoint), OpenTypeFont> _fallbacks = new();
    private readonly ConcurrentDictionary<OpenTypeFont, OpenTypeFont[]> _discovered = new();
    private readonly ConcurrentDictionary<(OpenTypeFont, ScriptTag, TypeFeatures), (int Index, int Value)[]> _lookups = new();

    [ThreadStatic]
    private static GlyphBuffer? _spareBuffer;

    // Held rather than converted from the method group on every call: text is measured on the hot path.
    private readonly Func<FontRequest, OpenTypeFont> _find;

    public TypeShaper(FontCatalog catalog, IReadOnlyList<string>? fallbackTypefaces = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _fallbackTypefaces = fallbackTypefaces ?? [];
        _find = Find;
    }

    /// <summary>The face a style's text is set in, before any fallback.</summary>
    public OpenTypeFont Resolve(TypeStyle style) => Resolve(RequestFor(style));

    /// <summary>Walks <paramref name="text"/> as glyphs set in <paramref name="style"/>.</summary>
    public GlyphWalk Walk(ReadOnlySpan<char> text, TypeStyle style)
    {
        FontRequest request = RequestFor(style);
        return new GlyphWalk(this, Resolve(request), request, text, style.EffectivePointSize, style.WordSpacing, style.Features);
    }

    /// <summary>
    /// The substitution lookups a face applies to text in <paramref name="script"/> with <paramref name="features"/>
    /// on top of the defaults, in the order they apply; empty for a face without any, which is set glyph for glyph.
    /// </summary>
    /// <remarks>
    /// A face whose GSUB table cannot be read is set without substitutions: its text still reads, where refusing the
    /// face would lose it.
    /// </remarks>
    internal IReadOnlyList<(int Index, int Value)> LookupsFor(OpenTypeFont face, ScriptTag script, TypeFeatures features)
    {
        if (face.Substitutions is null)
            return [];

        (OpenTypeFont, ScriptTag, TypeFeatures) key = (face, script, features);

        return _lookups.TryGetValue(key, out (int Index, int Value)[]? known) ? known : _lookups.GetOrAdd(key, ResolveLookups);
    }

    /// <summary>A buffer to shape with, kept per thread so that measuring text allocates nothing once warm.</summary>
    internal static GlyphBuffer RentBuffer()
    {
        GlyphBuffer? buffer = _spareBuffer;
        _spareBuffer = null;
        return buffer ?? new GlyphBuffer();
    }

    internal static void ReturnBuffer(GlyphBuffer buffer) => _spareBuffer = buffer;

    private static (int Index, int Value)[] ResolveLookups((OpenTypeFont Face, ScriptTag Script, TypeFeatures Features) key)
    {
        try
        {
            FeatureSetting[] settings = [.. GlyphSubstitutionTable.DefaultFeatures, .. key.Features.Settings];
            return [.. key.Face.Substitutions!.ResolveLookups(key.Script, LanguageTag.Default, settings)];
        }
        catch (FontFormatException)
        {
            return [];
        }
    }

    /// <summary>The face that sets <paramref name="codepoint"/>: the primary when it has the character.</summary>
    internal OpenTypeFont FaceFor(OpenTypeFont primary, FontRequest request, int codepoint)
    {
        if (primary.HasGlyph(codepoint))
            return primary;

        (OpenTypeFont, int) key = (primary, codepoint);

        if (_fallbacks.TryGetValue(key, out OpenTypeFont? known))
            return known;

        return _fallbacks.GetOrAdd(key, FindFallback(primary, request, codepoint));
    }

    private OpenTypeFont Resolve(FontRequest request) =>
        _faces.TryGetValue(request, out OpenTypeFont? face) ? face : _faces.GetOrAdd(request, _find);

    private static FontRequest RequestFor(TypeStyle style) =>
        new FontRequest(style.Typeface, (int)style.Weight, style.IsItalic ? FontSlant.Italic : FontSlant.Upright);

    private static IEnumerable<string> SubstitutesFor(string typeface)
    {
        string name = typeface.Trim();

        if (name.IndexOf("mono", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("courier", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.Equals("Consolas", StringComparison.OrdinalIgnoreCase))
        {
            return MonospaceSubstitutes;
        }

        if (name.IndexOf("times", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.Equals("serif", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Georgia", StringComparison.OrdinalIgnoreCase))
        {
            return SerifSubstitutes;
        }

        return SansSubstitutes;
    }

    private OpenTypeFont Find(FontRequest request)
    {
        if (Embeddable(_catalog.FindFace(request)) is FontFaceInfo face)
            return face.Load();

        foreach (string substitute in SubstitutesFor(request.Family))
        {
            if (Embeddable(_catalog.FindFace(request with { Family = substitute })) is FontFaceInfo stand)
                return stand.Load();
        }

        // Nothing like it anywhere: any registered face will set the text, which beats refusing the document, and
        // failing that the typefaces the package carries.
        if (_catalog.RegisteredFaces.FirstOrDefault(candidate => candidate.IsEmbeddable) is FontFaceInfo registered)
            return registered.Load();

        return BundledTypefaces.Match(request.Style);
    }

    private static FontFaceInfo? Embeddable(FontFaceInfo? face) => face is { IsEmbeddable: true } ? face : null;

    private OpenTypeFont FindFallback(OpenTypeFont primary, FontRequest request, int codepoint)
    {
        OpenTypeFont[] known = _discovered.GetOrAdd(primary, static _ => []);

        foreach (OpenTypeFont candidate in known)
        {
            if (candidate.HasGlyph(codepoint))
                return candidate;
        }

        OpenTypeFont? found = _catalog.FindFallback(codepoint, request, _fallbackTypefaces)
            ?? BundledTypefaces.Covering(codepoint, request.Style);

        // No face anywhere has the character, so the primary sets it as its missing-glyph box.
        if (found is null)
            return primary;

        // Rare and cheap next to the search above, so a lock is simpler than a lock-free swap.
        lock (_discovered)
        {
            OpenTypeFont[] faces = _discovered.GetOrAdd(primary, static _ => []);
            if (!faces.Contains(found))
                _discovered[primary] = [.. faces, found];
        }

        return found;
    }
}
