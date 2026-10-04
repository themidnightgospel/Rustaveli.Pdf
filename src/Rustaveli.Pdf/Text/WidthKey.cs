#if !NET
namespace Rustaveli.Pdf.Text;

/// <summary>
/// The text a width is kept under, on the build that has no lookup of a dictionary by characters: a string once the
/// width is kept, or, while one is looked up, the characters being measured, in a buffer the measurer reuses, so a
/// word measured before costs no string.
/// </summary>
internal readonly struct WidthKey
{
    private readonly string? _text;
    private readonly char[]? _probe;
    private readonly int _length;

    /// <summary>A key kept with its width.</summary>
    public WidthKey(string text) => _text = text;

    /// <summary>A key looked up and never kept: the first <paramref name="length"/> characters of <paramref name="probe"/>.</summary>
    public WidthKey(char[] probe, int length)
    {
        _probe = probe;
        _length = length;
    }

    /// <summary>Keys compared by their characters, ordinally.</summary>
    public static IEqualityComparer<WidthKey> Comparer { get; } = new ByCharacters();

    public ReadOnlySpan<char> Characters => _text is not null ? _text.AsSpan() : new ReadOnlySpan<char>(_probe, 0, _length);

    private sealed class ByCharacters : IEqualityComparer<WidthKey>
    {
        // Seeded per process, as the runtime seeds its own string hashes where it can, so that text cannot be made to
        // collide on purpose.
        private static readonly uint Seed = (uint)new Random().Next();

        public bool Equals(WidthKey x, WidthKey y) => x.Characters.SequenceEqual(y.Characters);

        public int GetHashCode(WidthKey key)
        {
            // FNV-1a over the UTF-16 code units, from a seeded start.
            uint hash = 2166136261u ^ Seed;

            foreach (char character in key.Characters)
                hash = (hash ^ character) * 16777619u;

            return (int)hash;
        }
    }
}
#endif
