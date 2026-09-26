using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>The committed fonts under tests/assets/fonts, for tests that check the parser against Skia.</summary>
internal static class FontAssets
{
    public static string PathOf(string fileName) => Path.Combine(AppContext.BaseDirectory, "assets", "fonts", fileName);

    public static OpenTypeFont Load(string fileName, int faceIndex = 0) =>
        OpenTypeFont.Load(File.ReadAllBytes(PathOf(fileName)), faceIndex);
}
