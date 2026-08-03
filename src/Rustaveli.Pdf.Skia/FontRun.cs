using SkiaSharp;

namespace Rustaveli.Pdf.Skia;

/// <summary>
/// A stretch of text that can be drawn with a single typeface.
/// </summary>
/// <remarks>
/// Produced by <see cref="SkiaFontProvider.Split"/> when a string needs more than one font — a Latin sentence
/// with a CJK name in it, say. Measurement and drawing consume the identical sequence, which is what keeps a
/// glyph and its advance in agreement.
/// </remarks>
internal readonly record struct FontRun(string Text, SKFont Font);
