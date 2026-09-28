using System.Collections.Concurrent;
using System.Globalization;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using Rustaveli.Pdf.Text.Bidi;

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
    private readonly ConcurrentDictionary<(OpenTypeFont Primary, TypefaceFallbacks Fallbacks, int Codepoint), OpenTypeFont> _fallbacks = new();
    private readonly ConcurrentDictionary<(OpenTypeFont Primary, TypefaceFallbacks Fallbacks), OpenTypeFont[]> _discovered = new();
    private readonly ConcurrentDictionary<(OpenTypeFont, ScriptTag, TypeFeatures), (int Index, int Value)[]> _lookups = new();

    [ThreadStatic]
    private static ShapingScratch? _spareScratch;

    // Held rather than converted from the method group on every call: text is measured on the hot path.
    private readonly Func<FontRequest, OpenTypeFont> _find;

    public TypeShaper(FontCatalog catalog, IReadOnlyList<string>? fallbackTypefaces = null, IComplexShaper? complex = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _fallbackTypefaces = fallbackTypefaces ?? [];
        Complex = complex;
        _find = Find;
    }

    /// <summary>The shaper runs in complex scripts are handed to; null to set them glyph for glyph.</summary>
    public IComplexShaper? Complex { get; }

    /// <summary>The face a style's text is set in, before any fallback.</summary>
    public OpenTypeFont Resolve(TypeStyle style) => Resolve(RequestFor(style));

    /// <summary>
    /// Walks <paramref name="text"/> as glyphs set in <paramref name="style"/>, in the order they are displayed from
    /// left to right.
    /// </summary>
    /// <param name="text">The text, in logical order.</param>
    /// <param name="style">The type it is set in.</param>
    /// <param name="rightToLeft">
    /// Whether the text reads right to left. It is still shaped in logical order — joining and ligatures depend on
    /// it — with each character that has a mirror image, such as a bracket, taken as that image (UAX #9, rule L4);
    /// then its glyphs are handed out last first.
    /// </param>
    public GlyphWalk Walk(ReadOnlySpan<char> text, TypeStyle style, bool rightToLeft = false)
    {
        FontRequest request = RequestFor(style);
        OpenTypeFont primary = Resolve(request);

        if (!rightToLeft)
            return new GlyphWalk(this, primary, request, text, style.EffectivePointSize, style.WordSpacing, style.Features, style.FallbackTypefaces);

        string mirrored = Mirrored(text);
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in new GlyphWalk(
            this, primary, request, mirrored.AsSpan(), style.EffectivePointSize, style.WordSpacing, style.Features, style.FallbackTypefaces))
        {
            glyphs.Add(glyph);
        }

        return GlyphWalk.Replaying(InDisplayOrder(glyphs));
    }

    /// <summary>
    /// Glyphs shaped in logical order, put in the order a right-to-left run displays them: cluster by cluster, last
    /// first, each cluster — a character, the marks that combine with it and any glyphs made of them — kept in its
    /// own order, so a mark still follows the letter it sits on.
    /// </summary>
    /// <remarks>
    /// A glyph's kerning is with the glyph before it in logical order. Between clusters that neighbour now follows it,
    /// so the kerning a cluster's first glyph carries moves to the first glyph of the cluster before it.
    /// </remarks>
    private static List<ShapedGlyph> InDisplayOrder(List<ShapedGlyph> logical)
    {
        List<(int Start, int Count)> clusters = [];

        for (int index = 0; index < logical.Count; index++)
        {
            if (clusters.Count > 0 && Continues(logical[index]))
                clusters[^1] = (clusters[^1].Start, clusters[^1].Count + 1);
            else
                clusters.Add((index, 1));
        }

        List<ShapedGlyph> display = new List<ShapedGlyph>(logical.Count);

        for (int cluster = clusters.Count - 1; cluster >= 0; cluster--)
        {
            (int start, int count) = clusters[cluster];
            float kerning = cluster + 1 < clusters.Count ? logical[clusters[cluster + 1].Start].Kerning : 0f;

            display.Add(logical[start] with { Kerning = kerning });

            for (int index = start + 1; index < start + count; index++)
                display.Add(logical[index]);
        }

        return display;
    }

    /// <summary>
    /// Whether a glyph belongs with the one before it: a further glyph of the same character, or a combining mark.
    /// </summary>
    private static bool Continues(ShapedGlyph glyph) =>
        glyph.Length == 0
        || (glyph.Codepoint <= char.MaxValue
            && CharUnicodeInfo.GetUnicodeCategory((char)glyph.Codepoint)
                is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark);

    /// <summary>The text with each character that has a mirror image replaced by it.</summary>
    private static string Mirrored(ReadOnlySpan<char> text)
    {
        char[] characters = text.ToArray();

        for (int index = 0; index < characters.Length; index++)
        {
            // Every mirrored pair lies in the Basic Multilingual Plane.
            int mirror = BidiCharacter.Mirror(characters[index]);

            if (mirror != characters[index] && mirror <= char.MaxValue)
                characters[index] = (char)mirror;
        }

        return new string(characters);
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

    /// <summary>
    /// What shaping needs, kept per thread so that measuring text allocates nothing once warm. A walk nested inside
    /// another, which the one spare cannot serve, gets its own.
    /// </summary>
    internal static ShapingScratch RentScratch()
    {
        ShapingScratch? scratch = _spareScratch;
        _spareScratch = null;
        return scratch ?? new ShapingScratch();
    }

    internal static void ReturnScratch(ShapingScratch scratch) => _spareScratch = scratch;

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
    internal OpenTypeFont FaceFor(OpenTypeFont primary, FontRequest request, TypefaceFallbacks fallbacks, int codepoint)
    {
        if (primary.HasGlyph(codepoint))
            return primary;

        (OpenTypeFont, TypefaceFallbacks, int) key = (primary, fallbacks, codepoint);

        if (_fallbacks.TryGetValue(key, out OpenTypeFont? known))
            return known;

        return _fallbacks.GetOrAdd(key, FindFallback(primary, request, fallbacks, codepoint));
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

        // The typeface the package carries, asked for by name, is the one wanted — not a stand-in for it.
        if (BundledTypefaces.Named(request.Family, request.Style) is OpenTypeFont bundled)
            return bundled;

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

    private OpenTypeFont FindFallback(OpenTypeFont primary, FontRequest request, TypefaceFallbacks fallbacks, int codepoint)
    {
        OpenTypeFont[] known = _discovered.GetOrAdd((primary, fallbacks), static _ => []);

        foreach (OpenTypeFont candidate in known)
        {
            if (candidate.HasGlyph(codepoint))
                return candidate;
        }

        // The style's own fallbacks come first, then the library's.
        IReadOnlyList<string> families = fallbacks.Names.Count == 0 ? _fallbackTypefaces : [.. fallbacks.Names, .. _fallbackTypefaces];
        OpenTypeFont? found = _catalog.FindFallback(codepoint, request, families)
            ?? BundledTypefaces.Covering(codepoint, request.Style);

        // No face anywhere has the character, so the primary sets it as its missing-glyph box.
        if (found is null)
            return primary;

        // Rare and cheap next to the search above, so a lock is simpler than a lock-free swap.
        lock (_discovered)
        {
            OpenTypeFont[] faces = _discovered.GetOrAdd((primary, fallbacks), static _ => []);
            if (!faces.Contains(found))
                _discovered[(primary, fallbacks)] = [.. faces, found];
        }

        return found;
    }
}
