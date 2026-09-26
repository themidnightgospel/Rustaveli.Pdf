using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// An image the Skia backend did not decode, standing in for one produced for some other backend.
/// </summary>
internal sealed class ForeignImage : IImage
{
    public int PixelWidth => 40;

    public int PixelHeight => 20;
}
