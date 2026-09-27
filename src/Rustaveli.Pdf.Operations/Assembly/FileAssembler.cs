using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Writes an assembled file: its pages copied from the files they came from, with their layers, and what the first
/// file says of the document as a whole.
/// </summary>
internal static class FileAssembler
{
    private static readonly PdfName Root = new PdfName("Root");
    private static readonly PdfName Info = PdfNames.Info;
    private static readonly PdfName Form = new PdfName("Form");
    private static readonly PdfName BBox = new PdfName("BBox");
    private static readonly PdfName CropBox = new PdfName("CropBox");
    private static readonly PdfName StructParents = new PdfName("StructParents");
    private static readonly PdfName EmbeddedFiles = new PdfName("EmbeddedFiles");

    /// <summary>What a document says of itself whatever pages it keeps.</summary>
    private static readonly PdfName[] Always =
    [
        new PdfName("Metadata"), new PdfName("Lang"), new PdfName("OutputIntents"), new PdfName("ViewerPreferences"),
        new PdfName("PageLayout"), new PdfName("Version"), new PdfName("Extensions"), new PdfName("AF"),
    ];

    public static void Write(IReadOnlyList<PageEntry> pages, PdfSource first, Stream output)
    {
        if (pages.Count == 0)
            throw new InvalidOperationException("A PDF needs at least one page; every page has been left out.");

        using PdfDocumentWriter writer = new PdfDocumentWriter(output, new PdfWriterOptions
        {
            CompressionLevel = CompressionLevel.Optimal,
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
        });

        ObjectCopier copier = new ObjectCopier(writer.File);
        List<(PdfReference Page, PdfReference Parent)> placed = pages.Select(_ => writer.AddPage()).ToList();

        // Every page of every file goes to the first page it became, or nowhere; the rest of their trees go nowhere.
        foreach (PdfSource source in pages.SelectMany(entry => entry.Over.Concat(entry.Beneath).Append(entry.Page)).Select(page => page.Source).Distinct())
        {
            foreach (int node in source.PageTree)
                copier.Redirect(source, node, null);

            if (source.Trailer.TryGetValue(Root, out PdfValue root) && root.Kind == PdfValueKind.Reference)
                copier.Redirect(source, root.AsReference().ObjectNumber, null);
        }

        // A page kept twice is referred to as the first it became.
        HashSet<(PdfSource, int)> assigned = [];

        for (int index = 0; index < pages.Count; index++)
        {
            SourcePage page = pages[index].Page;

            if (assigned.Add((page.Source, page.ObjectNumber)))
                copier.Redirect(page.Source, page.ObjectNumber, placed[index].Page);
        }

        // The first file's outline, names and structure point at its pages: they are kept while those pages are.
        bool whole = first.Pages.Count <= pages.Count
            && first.Pages.Select((page, index) => ReferenceEquals(pages[index].Page, page)).All(same => same);

        for (int index = 0; index < pages.Count; index++)
            WritePage(writer, copier, pages[index], placed[index], keepStructure: whole && ReferenceEquals(pages[index].Page.Source, first));

        CopyDocument(writer, copier, first, whole);
        copier.Flush();
        writer.Finish();
    }

    private static void WritePage(PdfDocumentWriter writer, ObjectCopier copier, PageEntry entry, (PdfReference Page, PdfReference Parent) placed, bool keepStructure)
    {
        SourcePage page = entry.Page;
        PdfSource source = page.Source;
        PdfName[] leaving = keepStructure ? [PdfNames.Parent] : [PdfNames.Parent, StructParents];
        PdfDictionary copied = copier.CopyDictionary(source, page.Dictionary, leaving);
        copied[PdfNames.Parent] = placed.Parent;

        if (entry.Beneath.Count > 0 || entry.Over.Count > 0)
            Layer(writer, copier, entry, copied);

        writer.File.Write(placed.Page, copied);
    }

    /// <summary>
    /// Draws the pages laid beneath and over a page around its own content, each as a form of its own, the page's
    /// content kept in a saved state so it cannot disturb what is drawn over it.
    /// </summary>
    private static void Layer(PdfDocumentWriter writer, ObjectCopier copier, PageEntry entry, PdfDictionary page)
    {
        PdfSource source = entry.Page.Source;
        PdfDictionary own = entry.Page.Dictionary;

        // Built from what the file holds, not from the copy: the copy's references number objects of the new file.
        PdfDictionary? held = own.TryGetValue(PdfNames.Resources, out PdfValue given) && source.Resolve(given) is { Kind: PdfValueKind.Dictionary } found
            ? found.AsDictionary()
            : null;

        PdfDictionary resources = held is null ? new PdfDictionary() : copier.CopyDictionary(source, held, PdfNames.XObject);
        PdfDictionary forms = held is not null && held.TryGetValue(PdfNames.XObject, out PdfValue existing) && source.Resolve(existing) is { Kind: PdfValueKind.Dictionary } named
            ? copier.CopyDictionary(source, named.AsDictionary())
            : new PdfDictionary();

        StringBuilder before = new StringBuilder();
        StringBuilder after = new StringBuilder("\nQ\n");
        int count = 0;

        foreach ((List<SourcePage> layers, StringBuilder content) in new[] { (entry.Beneath, before), (entry.Over, after) })
        {
            foreach (SourcePage layer in layers)
            {
                PdfName name;
                do
                {
                    name = new PdfName("Layer" + count++);
                }
                while (forms.ContainsKey(name));

                forms[name] = FormOf(writer, copier, layer);
                content.Append("q\n").Append(Encoding.ASCII.GetString(name.Encoded.ToArray())).Append(" Do\nQ\n");
            }
        }

        before.Append("q\n");
        resources[PdfNames.XObject] = forms;
        page[PdfNames.Resources] = resources;

        PdfArray contents = new PdfArray { writer.File.WriteStream(new PdfDictionary(), Encoding.ASCII.GetBytes(before.ToString())) };

        if (own.TryGetValue(PdfNames.Contents, out PdfValue streams))
        {
            if (source.Resolve(streams) is { Kind: PdfValueKind.Array } array)
            {
                foreach (PdfValue stream in array.AsArray())
                    contents.Add(copier.Copy(source, stream));
            }
            else
            {
                contents.Add(copier.Copy(source, streams));
            }
        }

        contents.Add(writer.File.WriteStream(new PdfDictionary(), Encoding.ASCII.GetBytes(after.ToString())));
        page[PdfNames.Contents] = contents;
    }

    /// <summary>A page as a form: its content, its resources, and its visible box as the form's bounds.</summary>
    private static PdfReference FormOf(PdfDocumentWriter writer, ObjectCopier copier, SourcePage page)
    {
        PdfSource source = page.Source;
        PdfValue box = page.Dictionary.TryGetValue(CropBox, out PdfValue crop) ? crop : page.Dictionary[PdfNames.MediaBox];
        PdfDictionary form = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.XObject,
            [PdfNames.Subtype] = Form,
            [BBox] = copier.Copy(source, source.Resolve(box)),
            [PdfNames.Resources] = page.Dictionary.TryGetValue(PdfNames.Resources, out PdfValue resources)
                ? copier.Copy(source, resources)
                : new PdfDictionary(),
        };

        using MemoryStream content = new MemoryStream();

        if (page.Dictionary.TryGetValue(PdfNames.Contents, out PdfValue contents))
        {
            IEnumerable<PdfValue> streams = source.Resolve(contents) is { Kind: PdfValueKind.Array } array ? array.AsArray().Cast<PdfValue>() : [contents];

            foreach (PdfValue stream in streams)
            {
                if (source.Stream(stream) is { } found)
                {
                    byte[] data = source.Decode(found);
                    content.Write(data, 0, data.Length);
                    content.WriteByte((byte)'\n');
                }
            }
        }

        return writer.File.WriteStream(form, content.ToArray());
    }

    private static void CopyDocument(PdfDocumentWriter writer, ObjectCopier copier, PdfSource first, bool whole)
    {
        PdfDictionary catalog = first.Catalog;

        foreach (KeyValuePair<PdfName, PdfValue> entry in catalog)
        {
            if (entry.Key.Equals(PdfNames.Type) || entry.Key.Equals(PdfNames.Pages))
                continue;

            if (whole || Array.IndexOf(Always, entry.Key) >= 0)
            {
                writer.Catalog[entry.Key] = copier.Copy(first, entry.Value);
            }
            else if (entry.Key.Equals(PdfNames.Names) && first.Resolve(entry.Value) is { Kind: PdfValueKind.Dictionary } names
                && names.AsDictionary().TryGetValue(EmbeddedFiles, out PdfValue files))
            {
                // Attached files belong to no page, so they stay whatever pages are kept.
                writer.Catalog[PdfNames.Names] = new PdfDictionary { [EmbeddedFiles] = copier.Copy(first, files) };
            }
        }

        if (first.Trailer.TryGetValue(Info, out PdfValue info) && first.Resolve(info) is { Kind: PdfValueKind.Dictionary } found)
            writer.InfoDictionary = copier.CopyDictionary(first, found.AsDictionary());
    }
}
