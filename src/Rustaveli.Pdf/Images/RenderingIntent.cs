namespace Rustaveli.Pdf.Images;

/// <summary>
/// The ICC rendering intent declared by a PNG sRGB chunk. Values match the chunk's encoding; the names match the
/// PDF /Intent values they correspond to.
/// </summary>
internal enum RenderingIntent
{
    Perceptual = 0,
    RelativeColorimetric = 1,
    Saturation = 2,
    AbsoluteColorimetric = 3,
}
