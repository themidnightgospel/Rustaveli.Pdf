namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A text measurer with fixed, arithmetic metrics.
/// </summary>
/// <remarks>
/// Layout assertions have to be exact, and real font metrics differ between machines and font versions. Every
/// character here is half the font size wide and every line exactly the font size tall, which makes expected
/// values calculable by hand: ten characters at size 10 measure 50 points.
/// </remarks>
internal sealed class FakeTypeMeasurer : ITypeMeasurer
{
    public const float CharacterWidthRatio = 0.5f;
    public const float AscentRatio = 0.8f;
    public const float DescentRatio = 0.2f;

    public TypeMetrics GetMetrics(TypeStyle style) => new(
        Ascent: style.EffectivePointSize * AscentRatio,
        Descent: style.EffectivePointSize * DescentRatio,
        LineGap: 0f);

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
