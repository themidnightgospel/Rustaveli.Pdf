using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>The committed fixtures under tests/assets/images; see the README there for what each one covers.</summary>
internal static class TestImageFiles
{
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    public static byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));

    public static RasterImage Load(string name) => RasterImage.FromFile(PathOf(name));

    public static PngFile Png(string name) => PngParser.Parse(Bytes(name));

    /// <summary>Every fixture that is a valid image, JPEG and PNG.</summary>
    public static IEnumerable<string> Valid() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "assets", "images"))
            .Select(Path.GetFileName)
            .Select(name => name!)
            .Where(name => (name.EndsWith(".png", StringComparison.Ordinal) || name.EndsWith(".jpg", StringComparison.Ordinal)) &&
                           !name.StartsWith("x", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);
}
