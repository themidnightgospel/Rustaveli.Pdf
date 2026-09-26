using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfPageTreeTests
{
    private static readonly PdfName Index = new PdfName("Index");

    private static PdfFileReader Build(int pageCount, out int treeCount)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfFileWriter writer = new PdfFileWriter(output, new PdfWriterOptions { CrossReferenceFormat = PdfCrossReferenceFormat.Table }))
        {
            PdfPageTree tree = new PdfPageTree(writer);
            for (int index = 0; index < pageCount; index++)
            {
                PdfReference page = writer.Reserve();
                PdfReference parent = tree.Add(page);
                writer.Write(page, new PdfDictionary { [PdfNames.Type] = PdfNames.Page, [PdfNames.Parent] = parent, [Index] = index });
            }

            treeCount = tree.Count;
            PdfReference root = tree.Write();
            writer.Finish(writer.Write(new PdfDictionary { [PdfNames.Type] = PdfNames.Catalog, [PdfNames.Pages] = root }));
        }

        return new PdfFileReader(output.ToArray());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(1024, 2)]
    [InlineData(1025, 3)]
    [InlineData(3000, 3)]
    public void KeepsEveryPageAtTheSameShallowDepthInOrder(int pageCount, int depth)
    {
        PdfFileReader reader = Build(pageCount, out int treeCount);

        List<(int Number, int Depth)> pages = reader.Pages();

        Assert.Equal(pageCount, treeCount);
        Assert.Equal(pageCount, pages.Count);
        Assert.All(pages, page => Assert.Equal(depth, page.Depth));
        Assert.Equal(
            Enumerable.Range(0, pageCount).Select(index => (long)index),
            pages.Select(page => (long)reader.Dictionary(new ParsedReference(page.Number, 0))["Index"]!));
    }

    [Fact]
    public void FillsEachLeafBeforeStartingTheNext()
    {
        PdfFileReader reader = Build(PdfPageTree.MaxKids + 1, out _);
        List<(int Number, int Depth)> pages = reader.Pages();

        Dictionary<string, object?> firstLeaf = reader.Dictionary(reader.Dictionary(new ParsedReference(pages[0].Number, 0))["Parent"]);
        Dictionary<string, object?> lastLeaf = reader.Dictionary(reader.Dictionary(new ParsedReference(pages[^1].Number, 0))["Parent"]);

        Assert.Equal(PdfPageTree.MaxKids, ((List<object?>)firstLeaf["Kids"]!).Count);
        Assert.Equal((long)PdfPageTree.MaxKids, firstLeaf["Count"]);
        Assert.Single((List<object?>)lastLeaf["Kids"]!);
        Assert.Equal(1L, lastLeaf["Count"]);
    }

    [Fact]
    public void GroupsLeavesUnderParentsOfAtMostThirtyTwo()
    {
        PdfFileReader reader = Build((PdfPageTree.MaxKids * PdfPageTree.MaxKids) + 1, out _);

        Dictionary<string, object?> root = reader.Dictionary(reader.Catalog()["Pages"]);
        List<object?> kids = (List<object?>)root["Kids"]!;

        Assert.Equal(2, kids.Count);
        Assert.Equal(PdfPageTree.MaxKids, ((List<object?>)reader.Dictionary(kids[0])["Kids"]!).Count);
        Assert.Equal((long)(PdfPageTree.MaxKids * PdfPageTree.MaxKids), reader.Dictionary(kids[0])["Count"]);
        Assert.Single((List<object?>)reader.Dictionary(kids[1])["Kids"]!);
        Assert.Equal((long)(PdfPageTree.MaxKids * PdfPageTree.MaxKids) + 1, root["Count"]);
    }

    [Fact]
    public void WritesAnEmptyRootForADocumentWithoutPages()
    {
        PdfFileReader reader = Build(0, out int treeCount);

        Dictionary<string, object?> root = reader.Dictionary(reader.Catalog()["Pages"]);

        Assert.Equal(0, treeCount);
        Assert.Empty(reader.Pages());
        Assert.Equal(new ParsedName("Pages"), root["Type"]);
        Assert.Empty((List<object?>)root["Kids"]!);
        Assert.Equal(0L, root["Count"]);
        Assert.False(root.ContainsKey("Parent"));
    }
}
