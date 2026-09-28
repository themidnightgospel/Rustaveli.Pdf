using SkiaSharp;

namespace Rustaveli.Pdf.Raster;

/// <summary>What a Skia surface draws its pages onto, and how each finished page is kept.</summary>
internal interface ISkiaPageTarget : IDisposable
{
    /// <summary>
    /// A canvas for a page of <paramref name="size"/> points, and how many of its units make a point across and down.
    /// </summary>
    SKCanvas Begin(Extent size, out SKPoint unitsPerPoint);

    /// <summary>The page just drawn, finished and encoded.</summary>
    byte[] End();

    /// <summary>Whether text is drawn as the outlines of its glyphs, for pages that must show without their fonts.</summary>
    bool TextAsOutlines { get; }
}
