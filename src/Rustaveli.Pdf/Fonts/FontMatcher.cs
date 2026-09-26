namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Chooses the face of a family closest to a requested style, by the CSS Fonts Level 4 font matching algorithm.
/// </summary>
/// <remarks>
/// <para>
/// CSS narrows the family in a fixed order — width, then slant, then weight — keeping at each step only the faces
/// that match best, so a condensed bold never beats a normal-width regular when a normal-width bold was asked for.
/// Ranking each face by the three criteria in that order and taking the lowest gives the same result.
/// </para>
/// <para>
/// Weight follows the CSS rule exactly: a weight between 400 and 500 first tries heavier weights up to 500, then
/// lighter ones, then heavier ones beyond 500; a lighter weight looks lighter first, a heavier one heavier first. So
/// a request for 600 against 400 and 700 gets 700, and one for 300 gets 400 only when nothing lighter exists.
/// </para>
/// </remarks>
internal static class FontMatcher
{
    /// <summary>The best face for <paramref name="style"/>; null only when there are no faces.</summary>
    public static FontFaceInfo? Select(IEnumerable<FontFaceInfo> faces, FaceStyle style)
    {
        FontFaceInfo? best = null;
        (int Width, int Slant, int Weight) bestRank = default;

        foreach (FontFaceInfo face in faces)
        {
            (int Width, int Slant, int Weight) rank = Rank(face.Style, style);

            // Strictly better only, so among equals the first listed wins and the choice is stable.
            if (best is null || rank.CompareTo(bestRank) < 0)
            {
                best = face;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>How far a face's style is from the requested one on each criterion, lower being closer.</summary>
    public static (int Width, int Slant, int Weight) Rank(FaceStyle face, FaceStyle requested) =>
        (WidthRank(face.Width, requested.Width),
         SlantRank(face.Slant, requested.Slant),
         WeightRank(face.Weight, requested.Weight));

    /// <summary>Normal or narrower requests look narrower first; wider requests look wider first.</summary>
    private static int WidthRank(int face, int requested)
    {
        int difference = face - requested;

        if (difference == 0)
            return 0;

        bool preferNarrower = requested <= FaceStyle.NormalWidth;
        bool isNarrower = difference < 0;

        return isNarrower == preferNarrower ? Math.Abs(difference) : 100 + Math.Abs(difference);
    }

    /// <summary>Italic falls back to oblique and oblique to italic before either falls back to upright.</summary>
    private static int SlantRank(FontSlant face, FontSlant requested)
    {
        if (face == requested)
            return 0;

        return requested switch
        {
            FontSlant.Upright => face == FontSlant.Oblique ? 1 : 2,
            _ => face == FontSlant.Upright ? 2 : 1
        };
    }

    private static int WeightRank(int face, int requested)
    {
        if (face == requested)
            return 0;

        const int Second = 10_000;
        const int Third = 20_000;
        int distance = Math.Abs(face - requested);

        if (requested is >= 400 and <= 500)
        {
            if (face > requested && face <= 500)
                return distance;

            return face < requested ? Second + distance : Third + distance;
        }

        bool preferLighter = requested < 400;
        bool isLighter = face < requested;

        return isLighter == preferLighter ? distance : Second + distance;
    }
}
