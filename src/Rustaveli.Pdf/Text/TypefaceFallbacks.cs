namespace Rustaveli.Pdf.Text;

/// <summary>
/// The typefaces a style falls back to, in order, for characters its own typeface lacks — compared by value, and
/// ignoring case as typeface names are matched, so that two styles naming the same fallbacks are equal.
/// </summary>
internal sealed class TypefaceFallbacks : IEquatable<TypefaceFallbacks>
{
    public static readonly TypefaceFallbacks None = new TypefaceFallbacks([]);

    private readonly string[] _names;

    private TypefaceFallbacks(string[] names)
    {
        _names = names;
    }

    /// <summary>The typefaces, in the order they are tried.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>A list of <paramref name="names"/>, blank ones left out; <see cref="None"/> when none remain.</summary>
    public static TypefaceFallbacks Of(IEnumerable<string> names)
    {
        string[] kept = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToArray();
        return kept.Length == 0 ? None : new TypefaceFallbacks(kept);
    }

    public bool Equals(TypefaceFallbacks? other)
    {
        if (other is null || other._names.Length != _names.Length)
            return false;

        for (int index = 0; index < _names.Length; index++)
        {
            if (!string.Equals(_names[index], other._names[index], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TypefaceFallbacks);

    public override int GetHashCode()
    {
        int hash = 17;

        foreach (string name in _names)
            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(name);

        return hash;
    }

    public override string ToString() => _names.Length == 0 ? "none" : string.Join(", ", _names);
}
