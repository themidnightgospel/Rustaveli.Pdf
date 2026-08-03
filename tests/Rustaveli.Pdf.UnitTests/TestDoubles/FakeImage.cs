using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A placeholder image of a known pixel size.
/// </summary>
public sealed class FakeImage(int width, int height) : IImage
{
    public int PixelWidth { get; } = width;

    public int PixelHeight { get; } = height;
}
