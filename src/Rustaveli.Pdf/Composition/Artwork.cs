using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Vector artwork: paths filled and stroked, text and transforms, drawn at a size of its own and placed in a frame as
/// an image is, scaled to fit. It stays vector in the PDF, sharp at any size.
/// </summary>
public sealed class Artwork
{
    private readonly IReadOnlyList<Action<ISurface, ITypeMeasurer>> _steps;

    internal Artwork(Extent size, IReadOnlyList<Action<ISurface, ITypeMeasurer>> steps)
    {
        Size = size;
        _steps = steps;
    }

    /// <summary>The artwork's own size, in points, before it is scaled to its frame.</summary>
    public Extent Size { get; }

    /// <summary>Artwork <paramref name="width"/> by <paramref name="height"/> points, drawn by <paramref name="draw"/>.</summary>
    public static Artwork Draw(float width, float height, Action<ArtworkComposer> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        if (!(width > 0) || !(height > 0) || float.IsInfinity(width) || float.IsInfinity(height))
            throw new ArgumentOutOfRangeException(nameof(width), "Artwork has a finite size greater than nothing each way.");

        ArtworkComposer composer = new ArtworkComposer();
        draw(composer);
        return new Artwork(new Extent(width, height), composer.Finish());
    }

    /// <summary>
    /// The artwork an SVG document draws, at the size it gives itself, a CSS pixel to three quarters of a point.
    /// </summary>
    /// <remarks>
    /// Shapes, paths, transforms, clip paths, linear gradients, text, embedded images and styles are drawn; radial
    /// gradients take the mean of their colours, and filters, masks, patterns and markers are left out. Nothing the
    /// document refers to outside itself is fetched.
    /// </remarks>
    /// <exception cref="FormatException">The text is not an SVG document.</exception>
    public static Artwork FromSvg(string svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        using StringReader reader = new StringReader(svg);
        return Svg.SvgReader.Read(reader);
    }

    /// <summary>
    /// The artwork the SVG document in <paramref name="svg"/> draws, read in the encoding the document declares, at the
    /// size it gives itself, a CSS pixel to three quarters of a point. The stream is read from where it stands and is
    /// left open: it is the caller's to dispose.
    /// </summary>
    /// <remarks><inheritdoc cref="FromSvg(string)" path="/remarks"/></remarks>
    /// <exception cref="FormatException">The bytes are not an SVG document.</exception>
    public static Artwork FromSvg(Stream svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        return Svg.SvgReader.Read(svg);
    }

    /// <inheritdoc cref="FromSvg(string)"/>
    public static Artwork FromSvgFile(string path)
    {
        using FileStream file = File.OpenRead(path);
        return FromSvg(file);
    }

    /// <summary>
    /// Draws the artwork at its own size, at the current origin, leaving the surface as it found it; text is measured
    /// with <paramref name="measurer"/> to be anchored.
    /// </summary>
    internal void Render(ISurface surface, ITypeMeasurer measurer)
    {
        surface.Save();

        foreach (Action<ISurface, ITypeMeasurer> step in _steps)
            step(surface, measurer);

        surface.Restore();
    }
}
