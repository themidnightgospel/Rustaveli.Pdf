using System.Reflection;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The typefaces the core package carries: Latin, Greek and Cyrillic subsets of Noto Sans in regular, bold, italic
/// and bold italic, used for text set in Noto Sans where no Noto Sans is registered or installed, and for any text
/// nothing registered or installed can set.
/// </summary>
/// <remarks>
/// A document naming Noto Sans is set in the same faces on every machine, and a machine with no fonts at all — a
/// minimal container image, say — still has something to set text in. The faces are loaded from the assembly on
/// first need and shared by every library.
/// </remarks>
internal static class BundledTypefaces
{
    private static readonly string[] Files = ["NotoSans-Regular.ttf", "NotoSans-Bold.ttf", "NotoSans-Italic.ttf", "NotoSans-BoldItalic.ttf"];

    private static readonly Lazy<FontFaceInfo[]> Loaded = new Lazy<FontFaceInfo[]>(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<FontFaceInfo> Faces => Loaded.Value;

    /// <summary>The bundled face nearest <paramref name="style"/>.</summary>
    public static OpenTypeFont Match(FaceStyle style) => FontMatcher.Select(Loaded.Value, style)!.Load();

    /// <summary>
    /// The bundled face nearest <paramref name="style"/> when <paramref name="family"/> names the bundled typeface;
    /// null for any other name.
    /// </summary>
    public static OpenTypeFont? Named(string family, FaceStyle style) =>
        FontMatcher.Select(Loaded.Value.Where(face => string.Equals(face.Names.PreferredFamily, family.Trim(), StringComparison.OrdinalIgnoreCase)), style)?.Load();

    /// <summary>A bundled face that has <paramref name="codepoint"/>, nearest <paramref name="style"/>; null when none has it.</summary>
    public static OpenTypeFont? Covering(int codepoint, FaceStyle style) =>
        FontMatcher.Select(Loaded.Value.Where(face => face.Covers(codepoint)), style)?.Load();

    private static FontFaceInfo[] Load()
    {
        Assembly assembly = typeof(BundledTypefaces).Assembly;

        return Files.Select(file =>
        {
            using Stream stream = assembly.GetManifestResourceStream("Rustaveli.Pdf.Fonts.Bundled." + file)!;
            using MemoryStream copy = new MemoryStream();
            stream.CopyTo(copy);

            byte[] data = copy.ToArray();
            return FontFaceInfo.FromFont(new FontFileSource(data), OpenTypeFont.Load(data), registered: false);
        }).ToArray();
    }
}
