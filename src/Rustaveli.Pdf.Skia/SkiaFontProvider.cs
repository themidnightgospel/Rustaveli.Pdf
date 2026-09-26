using System.Collections.Concurrent;
using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.Skia;

/// <summary>
/// Resolves <see cref="TextStyle"/> values to Skia typefaces and fonts, caching the results.
/// </summary>
/// <remarks>
/// Typeface lookup is comparatively expensive and the layout engine measures the same styles repeatedly, so both
/// levels are cached. Fonts registered from a stream take precedence over system fonts.
///
/// A provider is meant to be long-lived. Skia hands out shared typeface instances, and SkiaSharp's wrappers
/// release their native reference when finalised — so a provider that is created per document and then dropped
/// will, once collected, pull typefaces out from under any render still using them. The damage is silent: the
/// PDF stays structurally valid but its glyphs stop mapping to characters. Use <see cref="Shared"/>, or hold
/// your own instance for the lifetime of the process.
///
/// Requesting a font family the host does not have does not fail. Skia substitutes the closest match it can
/// find, so a document naming a font that is absent will lay out differently on a machine that has it.
/// </remarks>
public sealed class SkiaFontProvider : IDisposable
{
    private readonly ConcurrentDictionary<(string Family, int Weight, bool Italic), SKTypeface> _typefaces = new();
    private readonly ConcurrentDictionary<(string Family, int Weight, bool Italic, float Size), Lazy<SKFont>> _fonts = new();
    private readonly Dictionary<string, List<SKTypeface>> _registered = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SKTypeface> _owned = [];
    // Plain object rather than System.Threading.Lock, which is unavailable on netstandard2.0.
    private readonly object _registrationLock = new();

    /// <summary>
    /// The provider used when a caller supplies none.
    /// </summary>
    /// <remarks>
    /// Deliberately process-wide and never disposed. Skia shares typeface instances across the process, so
    /// creating and tearing down a provider per document corrupts any document being generated concurrently:
    /// disposing one provider's fonts releases typefaces another thread is still drawing with, and the output
    /// silently degrades to unmapped glyphs. Sharing one provider also avoids repeating font lookup for every
    /// document.
    /// </remarks>
    public static SkiaFontProvider Shared { get; } = new();

    /// <summary>
    /// Registers a font from a stream so documents can use typefaces that are not installed on the host.
    /// </summary>
    public void Register(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        SKTypeface typeface = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException("The stream does not contain a font Skia can read.");

        lock (_registrationLock)
        {
            _owned.Add(typeface);

            if (!_registered.TryGetValue(typeface.FamilyName, out List<SKTypeface>? family))
            {
                family = [];
                _registered[typeface.FamilyName] = family;
            }

            family.Add(typeface);

            // A newly registered family must take precedence over anything already resolved from system fonts.
            _typefaces.Clear();
            _fonts.Clear();
        }
    }

    public SKFont GetFont(TextStyle style)
    {
        (string Family, int Weight, bool Italic, float Size) key = (style.FontFamily, (int)style.Weight, style.IsItalic, style.EffectiveFontSize);

        // ConcurrentDictionary may run a GetOrAdd factory more than once under contention and discard the
        // losers. For unmanaged Skia handles that would leak, so creation is funnelled through a Lazy that
        // guarantees exactly one instance per key.
        Lazy<SKFont> font = _fonts.GetOrAdd(key, static (k, provider) => new Lazy<SKFont>(
            () => CreateLayoutFont(provider.GetTypeface(k.Family, k.Weight, k.Italic), k.Size),
            LazyThreadSafetyMode.ExecutionAndPublication), this);

        return font.Value;
    }

    public SKTypeface GetTypeface(TextStyle style) =>
        GetTypeface(style.FontFamily, (int)style.Weight, style.IsItalic);

    private SKTypeface GetTypeface(string family, int weight, bool italic) =>
        _typefaces.GetOrAdd((family, weight, italic), key =>
        {
            SKFontStyle style = new SKFontStyle(
                key.Weight,
                (int)SKFontStyleWidth.Normal,
                key.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            if (_registered.TryGetValue(key.Family, out List<SKTypeface>? candidates))
            {
                SKTypeface? match = candidates.FirstOrDefault(candidate =>
                    candidate.FontStyle.Slant == style.Slant && candidate.FontWeight == key.Weight);

                if (match is not null)
                    return match;

                if (candidates.Count > 0)
                    return candidates[0];
            }

            return SKTypeface.FromFamilyName(key.Family, style) ?? SKTypeface.Default;
        });

    /// <summary>
    /// Families consulted, in order, when the requested font has no glyph for a character.
    /// </summary>
    /// <remarks>
    /// Consulted ahead of the platform's own matching. Naming the fallbacks explicitly is what makes output
    /// reproducible: left to the system, the substitute chosen for a given character differs between machines.
    /// </remarks>
    public IList<string> FallbackFamilies { get; } = [];

    /// <summary>Typefaces already found to cover something the primary font could not, tried before matching again.</summary>
    private readonly ConcurrentDictionary<(string Family, int Weight, bool Italic), List<SKTypeface>> _discoveredFallbacks = new();

    private readonly object _fallbackLock = new();

    /// <summary>Coverage probes, one per typeface, kept because a font is needed to ask and building one is not free.</summary>
    private readonly ConcurrentDictionary<SKTypeface, SKFont> _coverageProbes = new();

    /// <summary>True when the typeface has a glyph for the character.</summary>
    private bool Covers(SKTypeface typeface, int codepoint)
    {
        SKFont probe = _coverageProbes.GetOrAdd(typeface, static face => new SKFont(face));

        return probe.GetGlyph(codepoint) != 0;
    }

    /// <summary>
    /// Splits text into the longest runs that can each be drawn with a single typeface.
    /// </summary>
    /// <remarks>
    /// Measurement and drawing both go through this, so the two cannot disagree about where a fallback begins —
    /// which is the failure mode that would otherwise put a glyph in one place and its advance in another.
    /// </remarks>
    internal IReadOnlyList<FontRun> Split(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        SKFont primary = GetFont(style);

        // Overwhelmingly the common case: one font covers everything, so avoid the per-character walk entirely.
        if (!NeedsFallback(text, primary.Typeface))
            return [new FontRun(text, primary)];

        List<FontRun> runs = new List<FontRun>();
        int start = 0;
        int index = 0;
        SKFont? currentFont = null;

        while (index < text.Length)
        {
            bool isPair = char.IsSurrogatePair(text, index);
            int codepoint = isPair ? char.ConvertToUtf32(text, index) : text[index];
            SKFont font = ResolveFont(codepoint, style, primary);

            if (currentFont is null)
            {
                currentFont = font;
            }
            else if (!ReferenceEquals(font, currentFont))
            {
                runs.Add(new FontRun(text[start..index], currentFont));
                start = index;
                currentFont = font;
            }

            index += isPair ? 2 : 1;
        }

        runs.Add(new FontRun(text[start..], currentFont ?? primary));

        return runs;
    }

    private bool NeedsFallback(string text, SKTypeface typeface)
    {
        int index = 0;

        while (index < text.Length)
        {
            bool isPair = char.IsSurrogatePair(text, index);
            int codepoint = isPair ? char.ConvertToUtf32(text, index) : text[index];

            // Whitespace and control characters are not worth chasing a fallback for; a missing space glyph
            // still advances correctly and substituting one would split runs pointlessly.
            if (!char.IsWhiteSpace((char)Math.Min(codepoint, char.MaxValue)) && !Covers(typeface, codepoint))
                return true;

            index += isPair ? 2 : 1;
        }

        return false;
    }

    /// <summary>
    /// Finds a font able to draw <paramref name="codepoint"/>, preferring the primary, then the configured
    /// fallbacks, then whatever the platform suggests.
    /// </summary>
    private SKFont ResolveFont(int codepoint, TextStyle style, SKFont primary)
    {
        if (Covers(primary.Typeface, codepoint))
            return primary;

        (string FontFamily, int, bool IsItalic) key = (style.FontFamily, (int)style.Weight, style.IsItalic);

        foreach (string family in FallbackFamilies)
        {
            SKTypeface candidate = GetTypeface(family, (int)style.Weight, style.IsItalic);

            if (Covers(candidate, codepoint))
                return FontFor(candidate, style);
        }

        // Fallbacks already discovered for this style are tried before asking the platform again: a run of CJK
        // resolves to the same face for every character, and matching is far more expensive than a coverage test.
        if (_discoveredFallbacks.TryGetValue(key, out List<SKTypeface>? discovered))
        {
            lock (_fallbackLock)
            {
                foreach (SKTypeface candidate in discovered)
                {
                    if (Covers(candidate, codepoint))
                        return FontFor(candidate, style);
                }
            }
        }

        SKTypeface? matched = SKFontManager.Default.MatchCharacter(
            style.FontFamily,
            (int)style.Weight,
            (int)SKFontStyleWidth.Normal,
            style.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright,
            null,
            codepoint);

        if (matched is null)
            return primary;

        List<SKTypeface> list = _discoveredFallbacks.GetOrAdd(key, static _ => []);

        lock (_fallbackLock)
        {
            if (!list.Contains(matched))
                list.Add(matched);
        }

        return FontFor(matched, style);
    }

    /// <summary>Builds a sized font over an already-resolved typeface, cached like any other.</summary>
    private SKFont FontFor(SKTypeface typeface, TextStyle style)
    {
        (string, int, bool IsItalic, float EffectiveFontSize) key = (typeface.FamilyName + "\0fallback", (int)style.Weight, style.IsItalic, style.EffectiveFontSize);

        Lazy<SKFont> font = _fonts.GetOrAdd(key, _ => new Lazy<SKFont>(
            () => CreateLayoutFont(typeface, style.EffectiveFontSize),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return font.Value;
    }

    /// <summary>
    /// Releases the fonts this provider created and the typefaces it loaded from streams.
    /// </summary>
    /// <remarks>
    /// Typefaces resolved from installed system fonts are deliberately left alone: Skia hands out shared
    /// instances, so releasing one here would pull it out from under any other provider — or any other thread —
    /// still using it.
    ///
    /// Disposing a provider while a document that used it is still being generated corrupts that document. Do
    /// not dispose a provider you have handed to a concurrent render.
    /// </remarks>
    /// <summary>A font for measuring and drawing text, with metrics that are the same on every platform.</summary>
    /// <remarks>
    /// Hinted advances come from the platform's rasteriser — DirectWrite, FreeType or Core Text — and differ for the
    /// same font file, so the same document spaced its words, and could break its lines, differently on a Linux
    /// server than on a Windows workstation. Unhinted linear metrics are the font's own design units, scaled, and
    /// identical everywhere.
    /// </remarks>
    private static SKFont CreateLayoutFont(SKTypeface typeface, float size) => new SKFont(typeface, size)
    {
        Hinting = SKFontHinting.None,
        LinearMetrics = true,
        Subpixel = true,
        Edging = SKFontEdging.SubpixelAntialias
    };

    public void Dispose()
    {
        foreach (Lazy<SKFont>? font in _fonts.Values.Where(font => font.IsValueCreated))
            font.Value.Dispose();

        foreach (SKTypeface typeface in _owned)
            typeface.Dispose();

        _fonts.Clear();
        _typefaces.Clear();
        _owned.Clear();
        _registered.Clear();
    }
}
