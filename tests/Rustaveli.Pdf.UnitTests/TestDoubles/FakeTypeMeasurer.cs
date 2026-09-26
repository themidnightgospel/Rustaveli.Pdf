namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A text measurer with fixed, arithmetic metrics.
/// </summary>
/// <remarks>
/// Layout assertions have to be exact, and real font metrics differ between machines and font versions. Every
/// character here is half the font size wide and every line exactly the font size tall, which makes expected
/// values calculable by hand: ten characters at size 10 measure 50 points.
/// </remarks>
/// <param name="placesStrokes">
/// Whether the font says where its underline and strike-through go and how thick they are, as most real fonts do;
/// without it they fall back to proportions of the type.
/// </param>
internal sealed class FakeTypeMeasurer(bool placesStrokes = false) : ITypeMeasurer
{
    public const float CharacterWidthRatio = 0.5f;
    public const float AscentRatio = 0.8f;
    public const float DescentRatio = 0.2f;
    public const float UnderlineOffsetRatio = 0.15f;
    public const float UnderlineWeightRatio = 0.05f;
    public const float StrikeHeightRatio = 0.25f;
    public const float StrikeWeightRatio = 0.06f;

    public TypeMetrics GetMetrics(TypeStyle style)
    {
        float size = style.EffectivePointSize;
        TypeMetrics metrics = new TypeMetrics(Ascent: size * AscentRatio, Descent: size * DescentRatio, LineGap: 0f);

        return placesStrokes
            ? metrics with
            {
                UnderlineOffset = size * UnderlineOffsetRatio,
                UnderlineWeight = size * UnderlineWeightRatio,
                StrikeHeight = size * StrikeHeightRatio,
                StrikeWeight = size * StrikeWeightRatio,
            }
            : metrics;
    }

    public float MeasureWidth(string text, TypeStyle style) =>
        string.IsNullOrEmpty(text) ? 0f : text.Length * CharacterWidth(style);

    public int MeasureCharactersFitting(string text, TypeStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return 0;

        int fitting = (int)Math.Floor(maxWidth / CharacterWidth(style));

        return Math.Clamp(fitting, 0, text.Length);
    }

    private static float CharacterWidth(TypeStyle style) => style.EffectivePointSize * CharacterWidthRatio;
}
