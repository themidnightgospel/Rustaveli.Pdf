using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// The OpenType features a style turns on or off beyond a shaper's defaults, compared by value so that two styles
/// asking for the same features are equal.
/// </summary>
/// <remarks>Immutable: each change returns a new set, as each change to a <see cref="TypeStyle"/> does.</remarks>
internal sealed class TypeFeatures : IEquatable<TypeFeatures>
{
    public static readonly TypeFeatures None = new TypeFeatures([]);

    private readonly FeatureSetting[] _settings;

    private TypeFeatures(FeatureSetting[] settings)
    {
        _settings = settings;
    }

    /// <summary>The settings, one per feature, in tag order.</summary>
    public IReadOnlyList<FeatureSetting> Settings => _settings;

    /// <summary>These settings with <paramref name="tag"/> set to <paramref name="value"/>, replacing any before.</summary>
    public TypeFeatures With(FeatureTag tag, int value)
    {
        List<FeatureSetting> settings = _settings.Where(setting => setting.Tag != tag).ToList();
        settings.Add(new FeatureSetting(tag, value));
        settings.Sort((left, right) => left.Tag.Value.CompareTo(right.Tag.Value));
        return new TypeFeatures(settings.ToArray());
    }

    public bool Equals(TypeFeatures? other) =>
        other is not null && _settings.AsSpan().SequenceEqual(other._settings);

    public override bool Equals(object? obj) => Equals(obj as TypeFeatures);

    public override int GetHashCode()
    {
        int hash = 17;

        foreach (FeatureSetting setting in _settings)
            hash = (hash * 31) + setting.GetHashCode();

        return hash;
    }

    public override string ToString() =>
        _settings.Length == 0 ? "none" : string.Join(" ", _settings.Select(setting => $"{setting.Tag}={setting.Value}"));
}
