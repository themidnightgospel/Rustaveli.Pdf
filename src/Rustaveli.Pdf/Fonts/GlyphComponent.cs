namespace Rustaveli.Pdf.Fonts;

/// <summary>One component of a composite TrueType glyph.</summary>
/// <param name="GlyphId">The glyph the component draws.</param>
/// <param name="GlyphIdOffset">
/// Where the component's glyph id is stored, from the start of the composite glyph's data: the one field a subsetter
/// rewrites when glyphs are renumbered.
/// </param>
internal readonly record struct GlyphComponent(ushort GlyphId, int GlyphIdOffset);
