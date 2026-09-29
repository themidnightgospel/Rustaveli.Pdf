using System.Runtime.CompilerServices;
using Rustaveli.Pdf.Fonts;
using Font = HarfBuzzSharp.Font;

namespace Rustaveli.Pdf.Shaping;

/// <summary>
/// The HarfBuzz font made for each face, made once however many shapers and threads ask for it, and kept for as long
/// as the face is.
/// </summary>
/// <remarks>
/// <para>
/// Each font holds a copy of its face's file in memory HarfBuzz owns, so one per face matters: every library, and a
/// library made per document or request makes a shaper of its own, shares <see cref="Shared"/>.
/// </para>
/// <para>
/// The fonts hang off their faces, weakly: once a face is collected so is its font, whose finalizer hands the copy
/// back — a font in use for a face still in use is never disposed from under a shaper. A font is made inside a lazy
/// value, so that threads first asking for it at once wait for one font rather than each making their own.
/// </para>
/// </remarks>
internal sealed class HarfBuzzFonts
{
    public static readonly HarfBuzzFonts Shared = new HarfBuzzFonts(HarfBuzzShaper.Create);

    private readonly ConditionalWeakTable<OpenTypeFont, Lazy<Font>> _fonts = new();

    // Held rather than made from a lambda on every miss.
    private readonly ConditionalWeakTable<OpenTypeFont, Lazy<Font>>.CreateValueCallback _make;

    public HarfBuzzFonts(Func<OpenTypeFont, Font> create)
    {
        _make = face => new Lazy<Font>(() => create(face), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Font For(OpenTypeFont face) =>
        (_fonts.TryGetValue(face, out Lazy<Font>? made) ? made : _fonts.GetValue(face, _make)).Value;
}
