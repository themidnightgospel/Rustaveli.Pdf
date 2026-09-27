using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf;

/// <summary>
/// Vector artwork: paths filled and stroked, text and transforms, drawn at a size of its own and placed in a frame as
/// an image is, scaled to fit. It stays vector in the PDF, sharp at any size.
/// </summary>
public sealed class Artwork
{
    private readonly IReadOnlyList<Action<ISurface>> _steps;

    internal Artwork(Extent size, IReadOnlyList<Action<ISurface>> steps)
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

    /// <summary>Draws the artwork at its own size, at the current origin, leaving the surface as it found it.</summary>
    internal void Render(ISurface surface)
    {
        surface.Save();

        foreach (Action<ISurface> step in _steps)
            step(surface);

        surface.Restore();
    }
}
