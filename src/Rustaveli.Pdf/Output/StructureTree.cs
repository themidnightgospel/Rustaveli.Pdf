using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Writes a tagged document's logical structure (ISO 32000-1, 14.7–14.8): an element for every part of the document
/// that holds content, each pointing at the marked content drawn for it, and the parent tree that leads back from
/// content to element.
/// </summary>
/// <remarks>
/// Elements that came to hold nothing — a table body whose rows were all repeats, a span of blank text — are left out,
/// so no reader meets an empty element.
/// </remarks>
internal static class StructureTree
{
    private static readonly PdfName StructTreeRoot = new PdfName("StructTreeRoot");
    private static readonly PdfName StructElem = new PdfName("StructElem");
    private static readonly PdfName ParentTree = new PdfName("ParentTree");
    private static readonly PdfName ParentTreeNextKey = new PdfName("ParentTreeNextKey");
    private static readonly PdfName Nums = new PdfName("Nums");
    private static readonly PdfName MarkInfo = new PdfName("MarkInfo");
    private static readonly PdfName Marked = new PdfName("Marked");
    private static readonly PdfName K = new PdfName("K");
    private static readonly PdfName P = new PdfName("P");
    private static readonly PdfName Pg = new PdfName("Pg");
    private static readonly PdfName Alt = new PdfName("Alt");
    private static readonly PdfName E = new PdfName("E");
    private static readonly PdfName Lang = new PdfName("Lang");
    private static readonly PdfName Mcr = new PdfName("MCR");
    private static readonly PdfName Mcid = new PdfName("MCID");
    private static readonly PdfName Objr = new PdfName("OBJR");
    private static readonly PdfName Obj = new PdfName("Obj");
    private static readonly PdfName O = new PdfName("O");
    private static readonly PdfName Table = new PdfName("Table");
    private static readonly PdfName Scope = new PdfName("Scope");
    private static readonly PdfName Row = new PdfName("Row");
    private static readonly PdfName Column = new PdfName("Column");
    private static readonly PdfName RowSpan = new PdfName("RowSpan");
    private static readonly PdfName ColSpan = new PdfName("ColSpan");

    /// <summary>
    /// Writes the tree under <paramref name="root"/>, with <paramref name="parents"/> — each page's elements by
    /// marked-content number, and each annotation's element, under their keys — and marks the document tagged.
    /// </summary>
    public static void Write(PdfDocumentWriter writer, StructureElement root, IReadOnlyDictionary<int, object> parents, int nextKey)
    {
        PdfFileWriter file = writer.File;
        Dictionary<StructureElement, PdfReference> references = [];
        Reserve(root, file, references);

        PdfReference tree = file.Reserve();

        if (references.ContainsKey(root))
            WriteElement(root, tree, file, references);

        PdfArray numbers = new PdfArray(parents.Count * 2);
        foreach (KeyValuePair<int, object> parent in parents.OrderBy(entry => entry.Key))
        {
            numbers.Add(parent.Key);

            if (parent.Value is List<StructureElement> marks)
            {
                PdfArray elements = new PdfArray(marks.Count);
                foreach (StructureElement element in marks)
                    elements.Add(references[element]);

                numbers.Add(elements);
            }
            else
            {
                numbers.Add(references[(StructureElement)parent.Value]);
            }
        }

        PdfDictionary treeRoot = new PdfDictionary
        {
            [PdfNames.Type] = StructTreeRoot,
            [ParentTree] = new PdfDictionary { [Nums] = numbers },
            [ParentTreeNextKey] = nextKey,
        };

        if (references.TryGetValue(root, out PdfReference document))
            treeRoot[K] = new PdfArray(1) { document };

        file.Write(tree, treeRoot);
        writer.Catalog[StructTreeRoot] = tree;
        writer.Catalog[MarkInfo] = new PdfDictionary { [Marked] = true };
    }

    /// <summary>Reserves an object for every element holding content, itself or below; returns whether this one does.</summary>
    private static bool Reserve(StructureElement element, PdfFileWriter file, Dictionary<StructureElement, PdfReference> references)
    {
        bool holds = false;

        foreach (object kid in element.Kids)
            holds |= kid is not StructureElement child || Reserve(child, file, references);

        if (holds)
            references.Add(element, file.Reserve());

        return holds;
    }

    private static void WriteElement(StructureElement element, PdfReference parent, PdfFileWriter file, Dictionary<StructureElement, PdfReference> references)
    {
        // Content on the element's first page is named by number alone; content elsewhere says which page it is on.
        PdfReference? page = element.Kids.OfType<MarkedContentReference>().Select(mark => (PdfReference?)mark.Page).FirstOrDefault();
        PdfArray kids = new PdfArray(element.Kids.Count);

        foreach (object kid in element.Kids)
        {
            switch (kid)
            {
                case StructureElement child when references.ContainsKey(child):
                    kids.Add(references[child]);
                    WriteElement(child, references[element], file, references);
                    break;

                case MarkedContentReference mark when mark.Page.Equals(page):
                    kids.Add(mark.Identifier);
                    break;

                case MarkedContentReference mark:
                    kids.Add(new PdfDictionary { [PdfNames.Type] = Mcr, [Pg] = mark.Page, [Mcid] = mark.Identifier });
                    break;

                case ObjectReference annotation:
                    kids.Add(new PdfDictionary { [PdfNames.Type] = Objr, [Obj] = annotation.Target, [Pg] = annotation.Page });
                    break;
            }
        }

        PdfDictionary dictionary = new PdfDictionary
        {
            [PdfNames.Type] = StructElem,
            [PdfNames.S] = new PdfName(element.Role),
            [P] = parent,
            [K] = kids,
        };

        if (page is { } first)
            dictionary[Pg] = first;

        if (element.AlternateText is { } alternate)
            dictionary[Alt] = PdfString.FromText(alternate);

        if (element.Expansion is { } expansion)
            dictionary[E] = PdfString.FromText(expansion);

        if (element.Language is { } language)
            dictionary[Lang] = PdfString.FromText(language);

        if (TableAttributes(element) is { } attributes)
            dictionary[PdfNames.A] = attributes;

        file.Write(references[element], dictionary);
    }

    /// <summary>What a table cell heads and spans, for a cell that heads others or spans more than one row or column.</summary>
    private static PdfDictionary? TableAttributes(StructureElement element)
    {
        if (element.Scope is null && element.RowSpan == 1 && element.ColumnSpan == 1)
            return null;

        PdfDictionary attributes = new PdfDictionary { [O] = Table };

        if (element.Scope is { } scope)
            attributes[Scope] = scope == TableScope.Row ? Row : Column;

        if (element.RowSpan > 1)
            attributes[RowSpan] = element.RowSpan;

        if (element.ColumnSpan > 1)
            attributes[ColSpan] = element.ColumnSpan;

        return attributes;
    }
}
