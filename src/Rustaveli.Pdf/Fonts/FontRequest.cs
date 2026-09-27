namespace Rustaveli.Pdf.Fonts;

/// <summary>A font asked for by family and style, as a document's type style names it.</summary>
/// <param name="Family">The family name, compared without regard to case.</param>
/// <param name="Weight">The CSS weight, 1 to 1000; 400 is regular and 700 bold.</param>
/// <param name="Slant">Upright, italic or oblique.</param>
/// <param name="Width">The width class, 1 to 9; 5 is normal.</param>
internal readonly record struct FontRequest(
    string Family,
    int Weight = FaceStyle.NormalWeight,
    FontSlant Slant = FontSlant.Upright,
    int Width = FaceStyle.NormalWidth)
{
    /// <summary>The style asked for, without the family.</summary>
    public FaceStyle Style => new FaceStyle(Weight, Width, Slant);
}
