namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// A run of glyphs in logical order, each with its cluster: the offset of the first character it came from. The
/// clusters let text extraction map the glyphs substitution produced back to the characters behind them.
/// </summary>
/// <remarks>
/// <para>
/// Built from text, a buffer starts with one glyph per character, each in a cluster of its own. Substitution keeps
/// clusters in order and never splits one: a ligature merges the clusters of its components, and the glyphs of a
/// multiple substitution share the cluster of the glyph they replace. So glyphs with equal clusters always stand
/// together for the characters from their cluster up to the next cluster (see <see cref="GetClusterEnd"/>) — "fi"
/// set as one ligature stands for both characters, and "ḿ" set as m and an accent is two glyphs for one — which is
/// exactly what a ToUnicode map or an ActualText span records.
/// </para>
/// <para>
/// Not thread-safe: a buffer belongs to the one piece of text being shaped.
/// </para>
/// </remarks>
internal sealed class GlyphBuffer
{
    private ushort[] _glyphs;
    private int[] _clusters;
    private int _count;

    public GlyphBuffer()
        : this(16)
    {
    }

    public GlyphBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _glyphs = new ushort[capacity];
        _clusters = new int[capacity];
    }

    public int Count => _count;

    /// <summary>How many glyphs the buffer holds before it must grow; growing at least doubles it.</summary>
    public int Capacity => _glyphs.Length;

    public ReadOnlySpan<ushort> Glyphs => _glyphs.AsSpan(0, _count);

    public ReadOnlySpan<int> Clusters => _clusters.AsSpan(0, _count);

    /// <summary>
    /// The font's glyph for each character of the text, in a cluster of its own: its offset in UTF-16 code units. A
    /// surrogate pair is one character; a lone surrogate maps as a character the font lacks.
    /// </summary>
    public static GlyphBuffer FromText(OpenTypeFont font, ReadOnlySpan<char> text)
    {
        GlyphBuffer buffer = new GlyphBuffer(text.Length);
        buffer.Load(font, text);
        return buffer;
    }

    /// <summary>Empties the buffer and fills it as <see cref="FromText"/> would, reusing its storage.</summary>
    public void Load(OpenTypeFont font, ReadOnlySpan<char> text)
    {
        ArgumentNullException.ThrowIfNull(font);
        _count = 0;
        EnsureCapacity(text.Length);
        int index = 0;

        while (index < text.Length)
        {
            int start = index;
            char character = text[index++];
            int codepoint = character;

            if (char.IsHighSurrogate(character) && index < text.Length && char.IsLowSurrogate(text[index]))
                codepoint = char.ConvertToUtf32(character, text[index++]);

            Add(font.GetGlyphId(codepoint), start);
        }
    }

    public void Add(ushort glyph, int cluster)
    {
        EnsureCapacity(_count + 1);
        _glyphs[_count] = glyph;
        _clusters[_count] = cluster;
        _count++;
    }

    /// <summary>
    /// Where the characters the glyph's cluster stands for end: the next cluster's value, or
    /// <paramref name="textLength"/> for the last cluster.
    /// </summary>
    public int GetClusterEnd(int index, int textLength)
    {
        ReadOnlySpan<int> clusters = Clusters;
        int cluster = clusters[index];

        for (int next = index + 1; next < clusters.Length; next++)
        {
            if (clusters[next] != cluster)
                return clusters[next];
        }

        return textLength;
    }

    // ---- Editing, for substitution -------------------------------------------------------------------------------

    public void Replace(int index, ushort glyph) => _glyphs[index] = glyph;

    /// <summary>
    /// Opens <paramref name="count"/> places after <paramref name="index"/>, each a copy of the glyph there and in its
    /// cluster, for a multiple substitution to fill.
    /// </summary>
    public void InsertAfter(int index, int count)
    {
        EnsureCapacity(_count + count);
        int tail = _count - index - 1;
        Array.Copy(_glyphs, index + 1, _glyphs, index + 1 + count, tail);
        Array.Copy(_clusters, index + 1, _clusters, index + 1 + count, tail);

        for (int offset = 1; offset <= count; offset++)
        {
            _glyphs[index + offset] = _glyphs[index];
            _clusters[index + offset] = _clusters[index];
        }

        _count += count;
    }

    public void RemoveAt(int index)
    {
        int tail = _count - index - 1;
        Array.Copy(_glyphs, index + 1, _glyphs, index, tail);
        Array.Copy(_clusters, index + 1, _clusters, index, tail);
        _count--;
    }

    /// <summary>
    /// Makes the glyphs from <paramref name="first"/> to <paramref name="last"/> one cluster, together with every
    /// glyph already sharing a cluster with either end, so no cluster is left split.
    /// </summary>
    public void MergeClusters(int first, int last)
    {
        while (first > 0 && _clusters[first - 1] == _clusters[first])
            first--;

        while (last < _count - 1 && _clusters[last + 1] == _clusters[last])
            last++;

        int cluster = _clusters[first];

        for (int index = first + 1; index <= last; index++)
            cluster = Math.Min(cluster, _clusters[index]);

        for (int index = first; index <= last; index++)
            _clusters[index] = cluster;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _glyphs.Length)
            return;

        int capacity = Math.Max(required, _glyphs.Length * 2);
        Array.Resize(ref _glyphs, capacity);
        Array.Resize(ref _clusters, capacity);
    }
}
