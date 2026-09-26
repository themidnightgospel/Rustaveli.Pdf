using System.Collections.Concurrent;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The fonts installed on the machine: found by scanning the font folders once, on first use, and shared by every
/// catalog built over the same index.
/// </summary>
/// <remarks>
/// <para>
/// Scanning reads each file's names and style, not its glyphs. Files that are not fonts this library reads, or that
/// cannot be read, are skipped: one broken file in a font folder must not stop documents from finding the rest.
/// </para>
/// <para>
/// Fallback — which installed face covers a character the requested font lacks — is cached per block of 128 code
/// points: the face found for one Georgian letter is tried first for the next, which almost always keeps a run of
/// one script in one face and turns the search into a single coverage check. Characters no face covers are
/// remembered too, so text full of them does not rescan every font for every character.
/// </para>
/// <para>
/// Every cache is a concurrent dictionary and every face loads at most once; nothing holds a lock across the
/// process, so documents generated in parallel do not wait on one another here.
/// </para>
/// </remarks>
internal sealed class SystemFontIndex
{
    private const int BlockShift = 7;

    private static readonly Lazy<SystemFontIndex> LazyCurrent =
        new Lazy<SystemFontIndex>(() => new SystemFontIndex(SystemFontDirectories.ForCurrentPlatform()));

    private readonly Lazy<FontFamilyIndex> _index;
    private readonly ConcurrentDictionary<FaceStyle, FontFaceInfo[]> _byStyle = new();
    private readonly ConcurrentDictionary<(FaceStyle Style, int Block), FontFaceInfo[]> _blocks = new();
    private readonly ConcurrentDictionary<(FaceStyle Style, int Codepoint), bool> _uncovered = new();

    public SystemFontIndex(IEnumerable<string> directories)
    {
        Directories = directories.ToArray();
        _index = new Lazy<FontFamilyIndex>(Scan, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The fonts installed on this machine, in the platform's usual font folders.</summary>
    public static SystemFontIndex Current => LazyCurrent.Value;

    /// <summary>No installed fonts at all: for output that must not depend on the machine it is produced on.</summary>
    public static SystemFontIndex Empty { get; } = new SystemFontIndex([]);

    public IReadOnlyList<string> Directories { get; }

    public IReadOnlyList<FontFaceInfo> Faces => _index.Value.Faces;

    public FontFamilyIndex Families => _index.Value;

    /// <summary>
    /// The installed face closest to <paramref name="style"/> that has a glyph for <paramref name="codepoint"/> and
    /// can be embedded in a PDF; null when none has.
    /// </summary>
    public FontFaceInfo? FindCovering(int codepoint, FaceStyle style)
    {
        (FaceStyle, int) block = (style, codepoint >> BlockShift);

        if (_blocks.TryGetValue(block, out FontFaceInfo[]? known))
        {
            foreach (FontFaceInfo face in known)
            {
                if (face.Covers(codepoint))
                    return face;
            }
        }

        if (_uncovered.ContainsKey((style, codepoint)))
            return null;

        foreach (FontFaceInfo face in OrderedFor(style))
        {
            if (face.IsEmbeddable && face.Covers(codepoint))
            {
                // Only threads racing on the same miss can list a face twice, which costs one repeated coverage
                // check and nothing else; a face already listed that covered this code point would have been found.
                _blocks.AddOrUpdate(block, [face], (_, faces) => [.. faces, face]);
                return face;
            }
        }

        _uncovered.TryAdd((style, codepoint), true);
        return null;
    }

    /// <summary>
    /// Every face, closest to the style first; among equally close faces, by family name, so the choice does not
    /// depend on the order the folders were read in.
    /// </summary>
    private FontFaceInfo[] OrderedFor(FaceStyle style) =>
        _byStyle.GetOrAdd(style, key => Faces
            .OrderBy(face => FontMatcher.Rank(face.Style, key))
            .ThenBy(face => face.Names.PreferredFamily, StringComparer.Ordinal)
            .ToArray());

    private FontFamilyIndex Scan()
    {
        List<FontFaceInfo> faces = [];

        foreach (string file in FontFileEnumerator.Enumerate(Directories))
        {
            try
            {
                faces.AddRange(FontFileScanner.Scan(file));
            }
            catch (Exception exception) when (
                exception is FontFormatException or IOException or UnauthorizedAccessException)
            {
                // Not a font this library reads, or not readable by this process; the other fonts still are.
            }
        }

        return new FontFamilyIndex(faces);
    }
}
