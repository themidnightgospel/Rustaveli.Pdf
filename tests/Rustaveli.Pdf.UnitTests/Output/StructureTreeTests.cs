using System.Text;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.UnitTests.Writing;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Output;

/// <summary>
/// A tagged document's structure as written: elements holding content, what each says of itself, and the parent tree
/// leading back from content to element.
/// </summary>
public class StructureTreeTests
{
    private sealed record Written(PdfFileReader Reader, Dictionary<string, object?> Tree, List<ParsedReference> Pages);

    /// <summary>
    /// Writes two pages and an annotation, lets <paramref name="build"/> make the structure over them, and reads it back.
    /// </summary>
    private static Written Write(Func<PdfReference[], PdfReference, (StructureElement Root, Dictionary<int, object> Parents, int NextKey)> build)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter document = new PdfDocumentWriter(output))
        {
            PdfPage first = document.BeginPage(100, 100);
            PdfPage second = document.BeginPage(100, 100);
            PdfReference annotation = document.File.Write(new PdfDictionary { [PdfNames.Type] = PdfNames.Annot });
            document.EndPage(first);
            document.EndPage(second);

            (StructureElement root, Dictionary<int, object> parents, int next) = build([first.Reference, second.Reference], annotation);
            StructureTree.Write(document, root, parents, next);
            document.Finish();
        }

        PdfFileReader reader = new PdfFileReader(output.ToArray());
        Dictionary<string, object?> catalog = reader.Catalog();
        List<ParsedReference> pages = reader.Pages().Select(page => new ParsedReference(page.Number, 0)).ToList();

        return new Written(reader, reader.Dictionary(catalog["StructTreeRoot"]), pages);
    }

    private static List<object?> Array(object? value) => Assert.IsType<List<object?>>(value);

    private static string Text(object? value) => Encoding.ASCII.GetString(Assert.IsType<byte[]>(value));

    [Fact]
    public void TheDocumentIsTheTreesOneChildAndTheDocumentIsMarkedTagged()
    {
        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            StructureElement paragraph = new StructureElement("P", root);
            root.Kids.Add(paragraph);
            paragraph.Kids.Add(new MarkedContentReference(pages[0], 0));
            return (root, new Dictionary<int, object> { [0] = new List<StructureElement> { paragraph } }, 1);
        });

        Dictionary<string, object?> catalog = written.Reader.Catalog();
        Assert.Equal(new ParsedName("StructTreeRoot"), written.Tree["Type"]);
        Assert.Equal(true, written.Reader.Dictionary(catalog["MarkInfo"])["Marked"]);
        Assert.Equal(1L, written.Tree["ParentTreeNextKey"]);

        Dictionary<string, object?> document = written.Reader.Dictionary(Assert.Single(Array(written.Tree["K"])));
        Assert.Equal(new ParsedName("StructElem"), document["Type"]);
        Assert.Equal(new ParsedName("Document"), document["S"]);
        Assert.Equal(catalog["StructTreeRoot"], document["P"]);
        Assert.False(document.ContainsKey("Pg"));

        Dictionary<string, object?> paragraph = written.Reader.Dictionary(Assert.Single(Array(document["K"])));
        Assert.Equal(new ParsedName("P"), paragraph["S"]);
        Assert.Equal(Array(written.Tree["K"])[0], paragraph["P"]);
        Assert.Equal(written.Pages[0], paragraph["Pg"]);
        Assert.Equal([0L], Array(paragraph["K"]));
    }

    [Fact]
    public void ContentOnOtherPagesAndAnnotationsSayWhereTheyAre()
    {
        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            StructureElement link = new StructureElement("Link", root);
            root.Kids.Add(link);
            link.Kids.Add(new MarkedContentReference(pages[0], 3));
            link.Kids.Add(new MarkedContentReference(pages[1], 0));
            link.Kids.Add(new ObjectReference(annotation, pages[1]));
            return (root, new Dictionary<int, object> { [1] = link }, 2);
        });

        Dictionary<string, object?> document = written.Reader.Dictionary(Array(written.Tree["K"])[0]);
        Dictionary<string, object?> link = written.Reader.Dictionary(Array(document["K"])[0]);
        List<object?> kids = Array(link["K"]);

        Assert.Equal(written.Pages[0], link["Pg"]);
        Assert.Equal(3L, kids[0]);

        Dictionary<string, object?> elsewhere = Assert.IsType<Dictionary<string, object?>>(kids[1]);
        Assert.Equal(new ParsedName("MCR"), elsewhere["Type"]);
        Assert.Equal(written.Pages[1], elsewhere["Pg"]);
        Assert.Equal(0L, elsewhere["MCID"]);

        Dictionary<string, object?> annotation = Assert.IsType<Dictionary<string, object?>>(kids[2]);
        Assert.Equal(new ParsedName("OBJR"), annotation["Type"]);
        Assert.Equal(written.Pages[1], annotation["Pg"]);
        Assert.Equal(new ParsedName("Annot"), written.Reader.Dictionary(annotation["Obj"])["Type"]);
    }

    [Fact]
    public void TheParentTreeLeadsFromContentBackToElementsInKeyOrder()
    {
        StructureElement? heading = null;
        StructureElement? link = null;

        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            heading = new StructureElement("H1", root);
            link = new StructureElement("Link", root);
            root.Kids.Add(heading);
            root.Kids.Add(link);
            heading.Kids.Add(new MarkedContentReference(pages[0], 0));
            heading.Kids.Add(new MarkedContentReference(pages[0], 1));
            link.Kids.Add(new ObjectReference(annotation, pages[0]));

            // Keys are listed in order however they were recorded.
            return (root, new Dictionary<int, object> { [1] = link, [0] = new List<StructureElement> { heading, heading } }, 2);
        });

        Dictionary<string, object?> document = written.Reader.Dictionary(Array(written.Tree["K"])[0]);
        List<object?> elements = Array(document["K"]);
        List<object?> numbers = Array(written.Reader.Dictionary(written.Tree["ParentTree"])["Nums"]);

        Assert.Equal(4, numbers.Count);
        Assert.Equal(0L, numbers[0]);
        Assert.Equal([elements[0], elements[0]], Array(numbers[1]));
        Assert.Equal(1L, numbers[2]);
        Assert.Equal(elements[1], numbers[3]);
    }

    [Fact]
    public void AnElementSaysWhatItIsReadAsAndInWhichLanguage()
    {
        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            StructureElement figure = new StructureElement("Figure", root) { AlternateText = "A chart", Language = "ka" };
            StructureElement abbreviation = new StructureElement("Span", root) { Expansion = "for example" };
            root.Kids.Add(figure);
            root.Kids.Add(abbreviation);
            figure.Kids.Add(new MarkedContentReference(pages[0], 0));
            abbreviation.Kids.Add(new MarkedContentReference(pages[0], 1));
            return (root, [], 1);
        });

        List<object?> elements = Array(written.Reader.Dictionary(Array(written.Tree["K"])[0])["K"]);
        Dictionary<string, object?> figure = written.Reader.Dictionary(elements[0]);
        Dictionary<string, object?> abbreviation = written.Reader.Dictionary(elements[1]);

        Assert.Equal("A chart", Text(figure["Alt"]));
        Assert.Equal("ka", Text(figure["Lang"]));
        Assert.False(figure.ContainsKey("E"));
        Assert.Equal("for example", Text(abbreviation["E"]));
        Assert.False(abbreviation.ContainsKey("Alt"));
        Assert.False(abbreviation.ContainsKey("Lang"));
        Assert.False(abbreviation.ContainsKey("A"));
    }

    [Fact]
    public void TableCellsSayWhatTheyHeadAndSpan()
    {
        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            StructureElement[] cells =
            [
                new StructureElement("TH", root) { Scope = TableScope.Column },
                new StructureElement("TH", root) { Scope = TableScope.Row, RowSpan = 2 },
                new StructureElement("TD", root) { ColumnSpan = 3 },
                new StructureElement("TD", root),
            ];

            for (int index = 0; index < cells.Length; index++)
            {
                root.Kids.Add(cells[index]);
                cells[index].Kids.Add(new MarkedContentReference(pages[0], index));
            }

            return (root, [], 1);
        });

        List<Dictionary<string, object?>> cells = Array(written.Reader.Dictionary(Array(written.Tree["K"])[0])["K"])
            .Select(written.Reader.Dictionary)
            .ToList();

        Dictionary<string, object?> column = written.Reader.Dictionary(cells[0]["A"]);
        Assert.Equal(new ParsedName("Table"), column["O"]);
        Assert.Equal(new ParsedName("Column"), column["Scope"]);
        Assert.False(column.ContainsKey("RowSpan"));
        Assert.False(column.ContainsKey("ColSpan"));

        Dictionary<string, object?> row = written.Reader.Dictionary(cells[1]["A"]);
        Assert.Equal(new ParsedName("Row"), row["Scope"]);
        Assert.Equal(2L, row["RowSpan"]);

        Dictionary<string, object?> wide = written.Reader.Dictionary(cells[2]["A"]);
        Assert.False(wide.ContainsKey("Scope"));
        Assert.Equal(3L, wide["ColSpan"]);

        Assert.False(cells[3].ContainsKey("A"));
    }

    [Fact]
    public void ElementsHoldingNothingAreLeftOut()
    {
        Written written = Write((pages, annotation) =>
        {
            StructureElement root = new StructureElement("Document", null);
            StructureElement table = new StructureElement("Table", root);
            StructureElement head = new StructureElement("THead", table);
            StructureElement body = new StructureElement("TBody", table);
            StructureElement row = new StructureElement("TR", body);
            StructureElement cell = new StructureElement("TD", row);
            root.Kids.Add(table);
            table.Kids.Add(head);
            table.Kids.Add(body);
            body.Kids.Add(row);
            row.Kids.Add(cell);
            row.Kids.Add(new StructureElement("TD", row));
            cell.Kids.Add(new MarkedContentReference(pages[0], 0));
            return (root, [], 1);
        });

        Dictionary<string, object?> document = written.Reader.Dictionary(Array(written.Tree["K"])[0]);
        Dictionary<string, object?> table = written.Reader.Dictionary(Assert.Single(Array(document["K"])));
        Dictionary<string, object?> body = written.Reader.Dictionary(Assert.Single(Array(table["K"])));
        Dictionary<string, object?> row = written.Reader.Dictionary(Assert.Single(Array(body["K"])));

        Assert.Equal(new ParsedName("TBody"), body["S"]);
        Assert.Single(Array(row["K"]));
    }

    [Fact]
    public void ADocumentHoldingNothingHasAnEmptyTree()
    {
        Written written = Write((pages, annotation) => (new StructureElement("Document", null), [], 0));

        Assert.False(written.Tree.ContainsKey("K"));
        Assert.Empty(Array(written.Reader.Dictionary(written.Tree["ParentTree"])["Nums"]));
        Assert.Equal(0L, written.Tree["ParentTreeNextKey"]);
    }
}
