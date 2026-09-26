namespace Rustaveli.Pdf.Fonts;

/// <summary>The style a face declares for itself: weight, width and slant.</summary>
/// <param name="Weight">The CSS weight scale, 1 to 1000, where 400 is regular and 700 bold.</param>
/// <param name="Width">1 (ultra-condensed) to 9 (ultra-expanded), where 5 is normal.</param>
/// <param name="Slant">Upright, italic or oblique.</param>
internal readonly record struct FaceStyle(int Weight, int Width, FontSlant Slant)
{
    public const int NormalWeight = 400;
    public const int BoldWeight = 700;
    public const int NormalWidth = 5;

    /// <summary>
    /// Reads the style from the <c>OS/2</c> table where the font has one, falling back to the bold and italic bits of
    /// <c>head</c>, which is all a font without one says about itself.
    /// </summary>
    public static FaceStyle From(Os2Table? os2, HeadTable head)
    {
        int weight = os2?.WeightClass ?? 0;

        // A few old fonts use the 1-9 scale of the Windows font dialog; 0 means the font did not say.
        if (weight is > 0 and < 10)
            weight *= 100;
        else if (weight == 0)
            weight = head.IsBold ? BoldWeight : NormalWeight;

        weight = Math.Min(weight, 1000);

        int width = os2?.WidthClass ?? NormalWidth;

        if (width is < 1 or > 9)
            width = NormalWidth;

        FontSlant slant = os2 switch
        {
            { IsOblique: true } => FontSlant.Oblique,
            { IsItalic: true } => FontSlant.Italic,
            _ => head.IsItalic ? FontSlant.Italic : FontSlant.Upright
        };

        return new FaceStyle(weight, width, slant);
    }
}
