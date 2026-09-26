using System.Reflection;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Decoding images from each of the sources the backend accepts.
/// </summary>
/// <remarks>
/// Every image is wider than it is tall, so a width and height swapped anywhere along the way cannot pass.
/// </remarks>
public class SkiaImageTests
{
    private static readonly byte[] NotAnImage = "certainly not an image"u8.ToArray();

    [Fact]
    public void FromBytesReportsTheDecodedPixelSize()
    {
        using SkiaImage image = SkiaImage.FromBytes(TestImages.Png(64, 32));

        Assert.Equal(64, image.PixelWidth);
        Assert.Equal(32, image.PixelHeight);
    }

    [Fact]
    public void FromBytesRejectsNull()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => SkiaImage.FromBytes(null!));

        Assert.Equal("data", error.ParamName);
    }

    [Fact]
    public void FromBytesRejectsDataThatIsNotAnImage()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => SkiaImage.FromBytes(NotAnImage));

        Assert.Contains("could not be decoded", error.Message);
    }

    [Fact]
    public void FromFileDecodesTheFileAtThePath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rustaveli-{Guid.NewGuid():N}.png");

        try
        {
            File.WriteAllBytes(path, TestImages.Png(48, 16));

            using SkiaImage image = SkiaImage.FromFile(path);

            Assert.Equal(48, image.PixelWidth);
            Assert.Equal(16, image.PixelHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileReportsAMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rustaveli-missing-{Guid.NewGuid():N}.png");

        Assert.Throws<FileNotFoundException>(() => SkiaImage.FromFile(path));
    }

    [Fact]
    public void FromStreamReadsFromTheCurrentPositionOfAStreamThatCannotSeek()
    {
        // Skia's decoder wants to seek; buffering first is what lets a network or compressed stream be used.
        byte[] png = TestImages.Png(40, 10);
        using MemoryStream source = new MemoryStream();
        source.Write([1, 2, 3], 0, 3);
        source.Write(png, 0, png.Length);
        source.Position = 3;

        using SkiaImage image = SkiaImage.FromStream(new ForwardOnlyStream(source));

        Assert.Equal(40, image.PixelWidth);
        Assert.Equal(10, image.PixelHeight);
    }

    [Fact]
    public void FromStreamRejectsNull()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => SkiaImage.FromStream(null!));

        Assert.Equal("stream", error.ParamName);
    }

    [Fact]
    public void FromStreamRejectsDataThatIsNotAnImage()
    {
        using MemoryStream stream = new MemoryStream(NotAnImage);

        Assert.Throws<InvalidOperationException>(() => SkiaImage.FromStream(stream));
    }

    [Fact]
    public void DisposeReleasesTheNativeImage()
    {
        // The released handle is the only trace disposal leaves: touching a released image through the public
        // surface is an access violation, not an exception. The wrapper is internal, and granting this assembly
        // the backend's internals would also expose the backend's polyfills, which collide with the core's.
        PropertyInfo? wrapped = typeof(SkiaImage).GetProperty("Image", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(wrapped is not null, "SkiaImage no longer keeps its decoded image in an 'Image' property.");

        SkiaImage image = SkiaImage.FromBytes(TestImages.Png(8, 4));
        SKImage native = Assert.IsType<SKImage>(wrapped!.GetValue(image));

        Assert.NotEqual(IntPtr.Zero, native.Handle);

        image.Dispose();

        Assert.Equal(IntPtr.Zero, native.Handle);
    }
}
