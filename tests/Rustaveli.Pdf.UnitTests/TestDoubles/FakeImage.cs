namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A placeholder image of a known pixel size.
/// </summary>
internal sealed class FakeImage(int width, int height) : IImage
{
    public int PixelWidth { get; } = width;

    public int PixelHeight { get; } = height;
}
