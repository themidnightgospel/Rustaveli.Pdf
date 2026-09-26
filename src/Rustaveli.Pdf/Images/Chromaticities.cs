namespace Rustaveli.Pdf.Images;

/// <summary>
/// The CIE 1931 xy chromaticities of an image's white point and primaries, from a PNG cHRM chunk. Informational:
/// the image is embedded in its device colour space regardless.
/// </summary>
internal sealed class Chromaticities(
    double whiteX,
    double whiteY,
    double redX,
    double redY,
    double greenX,
    double greenY,
    double blueX,
    double blueY)
{
    public double WhiteX { get; } = whiteX;

    public double WhiteY { get; } = whiteY;

    public double RedX { get; } = redX;

    public double RedY { get; } = redY;

    public double GreenX { get; } = greenX;

    public double GreenY { get; } = greenY;

    public double BlueX { get; } = blueX;

    public double BlueY { get; } = blueY;
}
