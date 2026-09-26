namespace Rustaveli.Pdf.Fonts;

/// <summary>Finds the font files under a set of folders, searching subfolders.</summary>
/// <remarks>
/// A folder that is missing or cannot be read is skipped, never an error: font folders differ between machines and
/// distributions, and one unreadable folder must not hide the fonts in the others. Depth is bounded because Unix
/// font folders are often symbolic-link farms, and a link back up the tree would otherwise never end.
/// </remarks>
internal static class FontFileEnumerator
{
    private const int MaximumDepth = 12;

    private static readonly string[] Extensions = [".ttf", ".otf", ".ttc", ".otc"];

    public static IReadOnlyList<string> Enumerate(IEnumerable<string> directories)
    {
        List<string> files = [];
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string directory in directories)
            Visit(directory, 0, files, seen);

        return files;
    }

    public static bool IsFontFile(string path)
    {
        string extension = Path.GetExtension(path);

        foreach (string candidate in Extensions)
        {
            if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void Visit(string directory, int depth, List<string> files, HashSet<string> seen)
    {
        string[] entries;
        string[] children;

        try
        {
            if (depth > MaximumDepth || !seen.Add(Path.GetFullPath(directory)))
                return;

            entries = Directory.GetFiles(directory);
            children = Directory.GetDirectories(directory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return;
        }

        // Sorted so the same folders always yield the same order, and so do the fonts chosen from them.
        Array.Sort(entries, StringComparer.Ordinal);
        Array.Sort(children, StringComparer.Ordinal);

        foreach (string file in entries)
        {
            if (IsFontFile(file) && seen.Add(Path.GetFullPath(file)))
                files.Add(file);
        }

        foreach (string child in children)
            Visit(child, depth + 1, files, seen);
    }
}
