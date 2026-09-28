namespace Rustaveli.Pdf.ConformanceTests.Specimens;

/// <summary>
/// Registers the committed Noto fonts once per test run, so specimens render with the same glyphs on every OS
/// instead of whatever the machine substitutes for a missing system font.
/// </summary>
internal static class TestFonts
{
    public const string Sans = "Noto Sans";

    private static readonly Lazy<bool> Registration = new Lazy<bool>(Register);

    public static void EnsureRegistered() => _ = Registration.Value;

    private static bool Register()
    {
        // TrueType and CFF faces both; none of the CFF ones covers a character any specimen sets in another face.
        foreach (string file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "fonts"))
                     .Where(file => file.EndsWith(".ttf", StringComparison.Ordinal) || file.EndsWith(".otf", StringComparison.Ordinal)))
            TypefaceLibrary.Shared.RegisterFile(file);

        // Arabic and Devanagari specimens need their scripts shaped; nothing else holds such characters.
        TypefaceLibrary.Shared.ShapeComplexScripts();
        return true;
    }
}
