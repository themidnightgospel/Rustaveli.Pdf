namespace Rustaveli.Pdf.Images;

/// <summary>The PNG colour types, numbered as in the IHDR chunk.</summary>
internal enum PngColorType
{
    Gray = 0,
    Rgb = 2,
    Palette = 3,
    GrayAlpha = 4,
    Rgba = 6,
}
