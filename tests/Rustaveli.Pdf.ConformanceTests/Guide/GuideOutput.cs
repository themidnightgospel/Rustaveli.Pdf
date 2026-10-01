using SkiaSharp;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>
/// Saves the page a guide's example makes as the image the guide shows beside it, in <c>docs/images/guide</c>.
/// </summary>
/// <remarks>
/// It writes only when <c>RUSTAVELI_GUIDE_IMAGES</c> is <c>1</c>, so an ordinary test run leaves the documentation
/// alone. After changing an example, refresh its image with
/// <c>RUSTAVELI_GUIDE_IMAGES=1 dotnet test tests/Rustaveli.Pdf.ConformanceTests --filter "FullyQualifiedName~.Guide."</c>.
/// </remarks>
internal static class GuideOutput
{
    /// <summary>Dots per inch: sharp in the output panel without making the repository heavy.</summary>
    private const float Resolution = 110;

    /// <summary>White space kept round a trimmed image, in pixels.</summary>
    private const int TrimMargin = 24;

    /// <summary>Saves page <paramref name="page"/> of <paramref name="document"/> as <c>docs/images/guide/<paramref name="name"/>.png</c>.</summary>
    /// <param name="document">The document the example composed.</param>
    /// <param name="name">The image's name, which the guide refers to it by.</param>
    /// <param name="page">The page to show, counted from 1.</param>
    /// <param name="trim">Cuts the page down to what is set on it, for an example that fills only part of a page.</param>
    /// <param name="typefaces">The typefaces the example exports with, when it registers its own.</param>
    public static void Show(Document document, string name, int page = 1, bool trim = false, TypefaceLibrary? typefaces = null)
    {
        if (Environment.GetEnvironmentVariable("RUSTAVELI_GUIDE_IMAGES") != "1")
            return;

        byte[] image = document.ExportImages(new ImageExportOptions { Resolution = Resolution, Typefaces = typefaces })[page - 1];
        string path = Path.Combine(RepositoryPaths.Root, "docs", "images", "guide", name + ".png");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, OnPaper(image, trim));
    }

    /// <summary>The page laid on white paper, as it prints: a page image is transparent wherever nothing is set.</summary>
    private static byte[] OnPaper(byte[] png, bool trim)
    {
        using SKBitmap page = SKBitmap.Decode(png);
        using SKBitmap paper = new SKBitmap(page.Width, page.Height);

        using (SKImage drawn = SKImage.FromBitmap(page))
        using (SKCanvas canvas = new SKCanvas(paper))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(drawn, 0, 0, SKSamplingOptions.Default);
        }

        SKRectI bounds = trim ? SetArea(paper) : new SKRectI(0, 0, paper.Width, paper.Height);

        using SKImage whole = SKImage.FromBitmap(paper);
        using SKImage cut = whole.Subset(bounds);
        using SKData data = cut.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>The smallest box round every pixel that is not paper, with a margin, inside the page.</summary>
    private static SKRectI SetArea(SKBitmap paper)
    {
        int left = paper.Width, top = paper.Height, right = -1, bottom = -1;

        for (int y = 0; y < paper.Height; y++)
        {
            for (int x = 0; x < paper.Width; x++)
            {
                SKColor pixel = paper.GetPixel(x, y);

                if (pixel.Red > 248 && pixel.Green > 248 && pixel.Blue > 248)
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
            return new SKRectI(0, 0, paper.Width, paper.Height);

        return new SKRectI(
            Math.Max(0, left - TrimMargin),
            Math.Max(0, top - TrimMargin),
            Math.Min(paper.Width, right + 1 + TrimMargin),
            Math.Min(paper.Height, bottom + 1 + TrimMargin));
    }
}
