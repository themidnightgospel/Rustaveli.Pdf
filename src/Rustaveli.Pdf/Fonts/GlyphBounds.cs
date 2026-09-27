namespace Rustaveli.Pdf.Fonts;

/// <summary>A glyph's bounding box in font units, as its <c>glyf</c> header records it.</summary>
internal readonly record struct GlyphBounds(short XMin, short YMin, short XMax, short YMax);
