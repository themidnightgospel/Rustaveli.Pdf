namespace Rustaveli.Pdf.Fonts;

/// <summary>Whether a face is upright or slanted, and how.</summary>
/// <remarks>
/// An italic is drawn as its own design, often with different letterforms; an oblique is the upright design sloped.
/// Font matching treats them as close substitutes for one another, as CSS does, but prefers the one asked for.
/// </remarks>
internal enum FontSlant
{
    Upright,
    Italic,
    Oblique
}
