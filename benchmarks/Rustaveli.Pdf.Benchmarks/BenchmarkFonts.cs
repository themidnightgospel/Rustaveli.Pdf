using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using Rustaveli.Pdf.Skia;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// Registers the committed test fonts with both libraries, once per process. A comparison where one library gets the
/// requested font and the other a system substitute measures the fonts, not the libraries.
/// </summary>
public static class BenchmarkFonts
{
    public const string Family = "Noto Sans";

    private static readonly Lazy<bool> Registration = new Lazy<bool>(Register);

    public static void EnsureRegistered() => _ = Registration.Value;

    private static bool Register()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;

        foreach (string file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "fonts"), "*.ttf"))
        {
            using (FileStream stream = File.OpenRead(file))
                FontManager.RegisterFont(stream);

            using (FileStream stream = File.OpenRead(file))
                SkiaFontProvider.Shared.Register(stream);
        }

        return true;
    }
}
