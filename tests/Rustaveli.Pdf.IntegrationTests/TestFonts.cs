using System.Runtime.CompilerServices;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The committed Noto Sans family (tests/assets/fonts), registered with both libraries before any test runs. A test
/// that names a system font gets a different substitute on every OS that lacks it — different advances, different
/// line and page breaks — so everything that measures or compares text names this family instead.
/// </summary>
/// <remarks>
/// Only the Latin family is registered. Fallback tests rely on the characters Noto Sans lacks (CJK, Georgian) being
/// found among the platform's fonts, which is the behaviour they exist to check.
/// </remarks>
public static class TestFonts
{
    public const string Sans = "Noto Sans";

    private static readonly string[] SansFiles = ["NotoSans-Regular.ttf", "NotoSans-Bold.ttf", "NotoSans-Italic.ttf"];

    public static string PathOf(string fileName) => Path.Combine(AppContext.BaseDirectory, "fonts", fileName);

    /// <summary>A provider of its own, with the test family registered, for tests that must not share state.</summary>
    public static SkiaFontProvider NewProvider()
    {
        SkiaFontProvider provider = new SkiaFontProvider();
        RegisterInto(provider);
        return provider;
    }

    public static void RegisterInto(SkiaFontProvider provider)
    {
        foreach (string file in SansFiles)
        {
            using FileStream stream = File.OpenRead(PathOf(file));
            provider.Register(stream);
        }
    }

    [ModuleInitializer]
    internal static void RegisterWithSharedProviders()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterInto(SkiaFontProvider.Shared);

        foreach (string file in SansFiles)
        {
            using FileStream stream = File.OpenRead(PathOf(file));
            FontManager.RegisterFont(stream);
        }
    }
}
