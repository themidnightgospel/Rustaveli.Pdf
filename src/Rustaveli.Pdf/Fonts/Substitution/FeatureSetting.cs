namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>Whether a feature applies, and for a feature offering alternates, which one.</summary>
/// <param name="Tag">The feature.</param>
/// <param name="Value">
/// 0 turns the feature off. Anything else turns it on; where a lookup offers alternates for a glyph, 1 chooses the
/// first, 2 the second and so on, and a value past the last chooses none.
/// </param>
internal readonly record struct FeatureSetting(FeatureTag Tag, int Value)
{
    public bool IsEnabled => Value != 0;

    public static FeatureSetting On(FeatureTag tag) => new FeatureSetting(tag, 1);

    public static FeatureSetting Off(FeatureTag tag) => new FeatureSetting(tag, 0);
}
