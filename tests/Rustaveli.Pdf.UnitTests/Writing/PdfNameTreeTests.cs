using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfNameTreeTests
{
    private static (PdfFileReader Reader, object? Root) Build(PdfNameTree tree)
    {
        using MemoryStream output = new MemoryStream();
        PdfReference root;
        using (PdfFileWriter writer = new PdfFileWriter(output))
        {
            root = tree.Write(writer);
            writer.Finish(writer.Write(new PdfDictionary { [PdfNames.Type] = PdfNames.Catalog }));
        }

        return (new PdfFileReader(output.ToArray()), new ParsedReference(root.ObjectNumber, 0));
    }

    private static PdfNameTree Numbered(int count)
    {
        PdfNameTree tree = new PdfNameTree();
        for (int index = count - 1; index >= 0; index--)
            tree.Add(PdfString.FromText($"k{index:D5}"), index);

        return tree;
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(32, 0)]
    [InlineData(33, 1)]
    [InlineData(1024, 1)]
    [InlineData(1025, 2)]
    [InlineData(2048, 2)]
    public void BalancesTheTreeWithSortedKeys(int count, int depth)
    {
        PdfNameTree tree = Numbered(count);
        (PdfFileReader reader, object? root) = Build(tree);

        List<(byte[] Key, object? Value, int Depth)> entries = reader.NameTree(root);

        Assert.Equal(count, tree.Count);
        Assert.Equal(Enumerable.Range(0, count).Select(index => $"k{index:D5}"), entries.Select(entry => Latin1.Text(entry.Key)));
        Assert.Equal(Enumerable.Range(0, count).Select(index => (object?)(long)index), entries.Select(entry => entry.Value));
        Assert.All(entries, entry => Assert.Equal(depth, entry.Depth));
    }

    [Fact]
    public void FillsLeavesToThirtyTwoEntries()
    {
        (PdfFileReader reader, object? root) = Build(Numbered(PdfNameTree.MaxEntries + 1));

        List<object?> kids = (List<object?>)reader.Dictionary(root)["Kids"]!;

        Assert.Equal(2, kids.Count);
        Assert.Equal(2 * PdfNameTree.MaxEntries, ((List<object?>)reader.Dictionary(kids[0])["Names"]!).Count);
        Assert.Equal(2, ((List<object?>)reader.Dictionary(kids[1])["Names"]!).Count);
    }

    [Fact]
    public void GroupsLeavesUnderParentsOfThirtyTwo()
    {
        (PdfFileReader reader, object? root) = Build(Numbered((PdfNameTree.MaxEntries * PdfNameTree.MaxEntries) + 1));

        List<object?> kids = (List<object?>)reader.Dictionary(root)["Kids"]!;

        Assert.Equal(2, kids.Count);
        Assert.Equal(PdfNameTree.MaxEntries, ((List<object?>)reader.Dictionary(kids[0])["Kids"]!).Count);
        Assert.Single((List<object?>)reader.Dictionary(kids[1])["Kids"]!);
    }

    [Fact]
    public void OrdersKeysByTheirEncodedBytes()
    {
        PdfNameTree tree = new PdfNameTree();
        foreach (string key in new[] { "ა", "b", "ä", "a b", "a", "B" })
            tree.Add(PdfString.FromText(key), 0);

        (PdfFileReader reader, object? root) = Build(tree);

        Assert.Equal(
            new[] { "B", "a", "a b", "b", "ä", "þÿ\u0010Ð" },
            reader.NameTree(root).Select(entry => Latin1.Text(entry.Key)));
    }

    [Fact]
    public void RefusesTheSameKeyTwice()
    {
        PdfNameTree tree = new PdfNameTree();
        tree.Add(PdfString.FromText("x"), 1);
        tree.Add(PdfString.FromText("y"), 2);
        tree.Add(PdfString.FromText("x"), 3);
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => tree.Write(writer));

        Assert.Equal("A name tree cannot hold the same key twice.", exception.Message);
    }

    [Fact]
    public void RefusesANullKey()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfNameTree().Add(null!, 1));
    }
}
