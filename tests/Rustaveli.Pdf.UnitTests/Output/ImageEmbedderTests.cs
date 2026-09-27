using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.UnitTests.Images;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Output;

public class ImageEmbedderTests
{
    private static string Embed(RasterImage image)
    {
        using MemoryStream stream = new MemoryStream();
        using (PdfFileWriter file = new PdfFileWriter(stream, new PdfWriterOptions { CompressionLevel = CompressionLevel.NoCompression }))
        {
            new ImageEmbedder(file).Reference(image);
            file.Finish(file.Write(new PdfDictionary()));
        }

        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void AnRgbJpegTellsTheReaderNotToTransformItsComponents()
    {
        // Components named R, G and B with no Adobe or JFIF marker are RGB as stored. A reader left to assume YCbCr
        // would convert them and shift every colour.
        RasterImage image = RasterImage.Load(TestJpeg.Build(TestJpeg.Frame(10, 20, [(byte)'R', (byte)'G', (byte)'B'])));

        Assert.Matches(@"/DecodeParms\s*<<\s*/ColorTransform 0\s*>>", Embed(image));
    }

    [Fact]
    public void AYCbCrJpegLeavesTheTransformToTheReader()
    {
        RasterImage image = RasterImage.Load(TestJpeg.Build(TestJpeg.Jfif(), TestJpeg.Frame(10, 20, 3)));

        Assert.DoesNotContain("/ColorTransform", Embed(image), StringComparison.Ordinal);
    }
}
