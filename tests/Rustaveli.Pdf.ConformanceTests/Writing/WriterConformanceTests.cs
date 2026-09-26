using System.IO.Compression;
using Rustaveli.Pdf.ConformanceTests.Validation;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.ConformanceTests.Writing;

/// <summary>
/// Files from the managed writer must satisfy qpdf's strict parser — every object, stream and cross-reference entry —
/// in both cross-reference forms, compressed and not.
/// </summary>
public class WriterConformanceTests
{
    private const string Clean = "No syntax or stream encoding errors found";

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void AShowcaseOfEveryFeaturePassesQpdf(string format)
    {
        byte[] pdf = WriterSamples.Showcase(WriterSamples.Options(format));

        Assert.Contains(Clean, Qpdf.Check(pdf), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void AnUncompressedShowcasePassesQpdf(string format)
    {
        byte[] pdf = WriterSamples.Showcase(WriterSamples.Options(format, CompressionLevel.NoCompression));

        Assert.Contains(Clean, Qpdf.Check(pdf), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void ADocumentDeepEnoughForThreeTreeLevelsPassesQpdf(string format)
    {
        // Over 32 × 32 pages and names, so both the page tree and the name tree need a middle level.
        byte[] pdf = WriterSamples.ManyPages(WriterSamples.Options(format), 1100);

        string report = Qpdf.Check(pdf);

        Assert.Contains(Clean, report, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomObjectGraphsPassQpdf(int seed, string format)
    {
        Random random = new Random(seed);
        byte[] pdf = WriterSamples.Write(WriterSamples.Options(format), document =>
        {
            RandomObjects objects = new RandomObjects(random, document.File);
            PdfArray all = new PdfArray();
            for (int index = 0; index < 40; index++)
                all.Add(objects.WriteIndirect());

            document.Catalog[new PdfName("RandomObjects")] = all;
            document.EndPage(document.BeginPage(100, 100));
        });

        Assert.Contains(Clean, Qpdf.Check(pdf), StringComparison.Ordinal);
    }

    public static TheoryData<int, string> Seeds()
    {
        TheoryData<int, string> seeds = new TheoryData<int, string>();
        for (int seed = 1; seed <= 12; seed++)
            seeds.Add(seed, seed % 2 == 0 ? "Stream" : "Table");

        return seeds;
    }
}
