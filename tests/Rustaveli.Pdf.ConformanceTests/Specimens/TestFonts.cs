using Rustaveli.Pdf.Skia;

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
        foreach (string file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "fonts"), "*.ttf"))
        {
            using FileStream stream = File.OpenRead(file);
            SkiaFontProvider.Shared.Register(stream);
        }

        return true;
    }
}
