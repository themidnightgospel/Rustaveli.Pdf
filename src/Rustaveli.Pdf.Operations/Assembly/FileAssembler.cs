using System.Globalization;
using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Operations.Linearization;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Security;
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
    private static readonly PdfName Rotate = new PdfName("Rotate");
    private static readonly PdfName AcroForm = new PdfName("AcroForm");
    private static readonly PdfName Fields = new PdfName("Fields");
    private static readonly PdfName DR = new PdfName("DR");
    private static readonly PdfName T = new PdfName("T");
    private static readonly PdfName OCProperties = new PdfName("OCProperties");
    private static readonly PdfName OCGs = new PdfName("OCGs");
    private static readonly PdfName BaseState = new PdfName("BaseState");
    private static readonly PdfName On = new PdfName("ON");
    private static readonly PdfName Off = new PdfName("OFF");

    /// <summary>What an optional content configuration lists that every file's configuration adds to.</summary>
    private static readonly PdfName[] Joined = [new PdfName("Order"), new PdfName("RBGroups"), new PdfName("Locked")];
    private static readonly PdfName MarkInfo = new PdfName("MarkInfo");
    private static readonly PdfName Marked = new PdfName("Marked");
    private static readonly PdfName StructParents = new PdfName("StructParents");
    private static readonly PdfName StructParent = new PdfName("StructParent");
    private static readonly PdfName P = new PdfName("P");
    private static readonly PdfName EmbeddedFiles = new PdfName("EmbeddedFiles");
    private static readonly PdfName Associated = new PdfName("AF");
    private static readonly PdfName Perms = new PdfName("Perms");
    private static readonly PdfName Metadata = new PdfName("Metadata");
    private static readonly PdfName Xml = new PdfName("XML");

    /// <summary>What a document says of itself whatever pages it keeps.</summary>
    private static readonly PdfName[] Always =
    [
        new PdfName("Metadata"), new PdfName("Lang"), new PdfName("OutputIntents"), new PdfName("ViewerPreferences"),
        new PdfName("PageLayout"), new PdfName("Version"), new PdfName("Extensions"),
    ];

    public static void Write(IReadOnlyList<PageEntry> pages, PdfSource first, Stream output, SaveSettings settings)
    {
        if (pages.Count == 0)
            throw new InvalidOperationException("A PDF needs at least one page; every page has been left out.");

        PdfEncryption? encryption = settings.Protection is { } protection
            ? PdfEncryption.Create(protection)
            : settings.KeepProtection ? first.Encryption : null;

        if (!settings.Linearize)
        {
            Write(pages, first, output, settings, encryption);
            return;
        }

        // Linearizing lays the finished file out anew, so it is finished plain first and encrypted as it is laid out.
        using MemoryStream plain = new MemoryStream();
        Write(pages, first, plain, settings, encryption: null);
        Linearizer.Write(plain.ToArray(), encryption, output);
    }

    private static void Write(IReadOnlyList<PageEntry> pages, PdfSource first, Stream output, SaveSettings settings, PdfEncryption? encryption)
    {
        using PdfDocumentWriter writer = new PdfDocumentWriter(output, new PdfWriterOptions
        {
            CompressionLevel = CompressionLevel.Optimal,
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
            Encryption = encryption,
        });

        ObjectCopier copier = new ObjectCopier(writer.File);
        List<(PdfReference Page, PdfReference Parent)> placed = pages.Select(_ => writer.AddPage()).ToList();

        // Every file the pages come from, the first file first whether or not any of its pages are kept.
        List<PdfSource> sources = new[] { first }
            .Concat(pages.SelectMany(entry => entry.Over.Concat(entry.Beneath).Append(entry.Page)).Select(page => page.Source))
            .Distinct()
            .ToList();

        // Every page of every file goes to the first page it became, or nowhere; the rest of their trees go nowhere.
        foreach (PdfSource source in sources)
        {
            foreach (int node in source.PageTree)
                copier.Redirect(source, node, null);

            if (source.Trailer.TryGetValue(Root, out PdfValue root) && root.Kind == PdfValueKind.Reference)
                copier.Redirect(source, root.AsReference().ObjectNumber, null);
        }

        // A page kept twice is referred to as the first it became.
        HashSet<(PdfSource, int)> assigned = [];
        bool[] again = new bool[pages.Count];

        for (int index = 0; index < pages.Count; index++)
        {
            SourcePage page = pages[index].Page;
            again[index] = !assigned.Add((page.Source, page.ObjectNumber));

            if (!again[index])
                copier.Redirect(page.Source, page.ObjectNumber, placed[index].Page);
        }

        // The first file's outline, names and structure point at its pages: they are kept while those pages are.
        bool whole = first.Pages.Count <= pages.Count
            && first.Pages.Select((page, index) => ReferenceEquals(pages[index].Page, page)).All(same => same);

        // Only the first file's named destinations are kept, and only then: every other file's links name destinations
        // of its own, which the names kept would take elsewhere or nowhere, so they are copied as the places they name.
        foreach (PdfSource source in pages.Select(entry => entry.Page.Source).Distinct())
        {
            if (!whole || !ReferenceEquals(source, first))
                copier.ResolveNamedDestinations(source);
        }

        // A page laid on many — a letterhead under every page — is written as a form once, and drawn wherever it is laid.
        Dictionary<(PdfSource, int), PdfReference> forms = [];

        for (int index = 0; index < pages.Count; index++)
            WritePage(writer, copier, pages[index], placed[index], keepStructure: whole && ReferenceEquals(pages[index].Page.Source, first), forms, again[index]);

        CopyDocument(writer, copier, first, whole, settings, sources, pages);
        copier.Flush();
        writer.Finish();
    }

    private static void WritePage(
        PdfDocumentWriter writer,
        ObjectCopier copier,
        PageEntry entry,
        (PdfReference Page, PdfReference Parent) placed,
        bool keepStructure,
        Dictionary<(PdfSource, int), PdfReference> forms,
        bool again)
    {
        SourcePage page = entry.Page;
        PdfSource source = page.Source;

        // The structure tree knows a page kept twice only as the first it became, whose place in it the copy must not claim.
        PdfName[] leaving = keepStructure && !again ? [PdfNames.Parent] : [PdfNames.Parent, StructParents];
        PdfDictionary copied = copier.CopyDictionary(source, page.Dictionary, leaving);
        copied[PdfNames.Parent] = placed.Parent;

        if (again && page.Dictionary.TryGetValue(PdfNames.Annots, out PdfValue listed) && source.Resolve(listed) is { Kind: PdfValueKind.Array } annotations)
            copied[PdfNames.Annots] = CopyAnnotations(writer, copier, source, annotations.AsArray(), placed.Page);

        if (entry.Beneath.Count > 0 || entry.Over.Count > 0)
            Layer(writer, copier, entry, copied, forms);

        writer.File.Write(placed.Page, copied);
    }

    /// <summary>
    /// The annotations of a page kept again, each written anew for <paramref name="page"/>: an annotation belongs to
    /// one page, which its <c>/P</c> names, and one place in the structure, which the first copy keeps.
    /// </summary>
    private static PdfArray CopyAnnotations(PdfDocumentWriter writer, ObjectCopier copier, PdfSource source, PdfArray annotations, PdfReference page)
    {
        PdfArray copies = new PdfArray(annotations.Count);

        foreach (PdfValue item in annotations)
        {
            if (source.Resolve(item) is { Kind: PdfValueKind.Dictionary } annotation)
            {
                PdfDictionary copy = copier.CopyDictionary(source, annotation.AsDictionary(), StructParent, P);
                copy[P] = page;
                copies.Add(writer.File.Write(copy));
            }
            else
            {
                copies.Add(copier.Copy(source, item));
            }
        }

        return copies;
    }

    /// <summary>
    /// Draws the pages laid beneath and over a page around its own content, each as a form of its own, the page's
    /// content kept in a saved state so it cannot disturb what is drawn over it.
    /// </summary>
    private static void Layer(PdfDocumentWriter writer, ObjectCopier copier, PageEntry entry, PdfDictionary page, Dictionary<(PdfSource, int), PdfReference> written)
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

        // On a tagged page everything drawn is either in the structure or marked as not being content: what is laid
        // on it is neither the page's text nor any part of its structure, so it is an artifact.
        bool artifact = own.ContainsKey(StructParents) || IsMarked(source);

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

                if (!written.TryGetValue((layer.Source, layer.ObjectNumber), out PdfReference form))
                {
                    form = FormOf(writer, copier, layer);
                    written[(layer.Source, layer.ObjectNumber)] = form;
                }

                forms[name] = form;
                content.Append(artifact ? "/Artifact BMC\nq\n" : "q\n")
                    .Append(Placement(layer, entry.Page))
                    .Append(Encoding.ASCII.GetString(name.Encoded.ToArray()))
                    .Append(artifact ? " Do\nQ\nEMC\n" : " Do\nQ\n");
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

    /// <summary>
    /// The <c>cm</c> that draws <paramref name="layer"/> on <paramref name="target"/> as it is seen on its own page:
    /// turned by its own rotation and back by the target's, so that it reads as it did once the target is turned in
    /// its turn, then centred on the target's visible box and shrunk to fit it if larger, as qpdf places pages. Nothing
    /// where that leaves it where it is — neither turned, and boxes alike.
    /// </summary>
    private static string Placement(SourcePage layer, SourcePage target)
    {
        if (VisibleBox(layer) is not { } from || VisibleBox(target) is not { } to)
            return string.Empty;

        // /Rotate turns a page clockwise as it is shown; a clockwise turn maps (x, y) to (x cos + y sin, y cos - x sin).
        int turn = (((Rotation(layer) - Rotation(target)) % 360) + 360) % 360;
        (double a, double b, double c, double d) = turn switch
        {
            90 => (0d, -1d, 1d, 0d),
            180 => (-1d, 0d, 0d, -1d),
            270 => (0d, 1d, -1d, 0d),
            _ => (1d, 0d, 0d, 1d),
        };

        bool across = turn is 90 or 270;
        double width = across ? from.Height : from.Width;
        double height = across ? from.Width : from.Height;
        double scale = Math.Min(1, Math.Min(to.Width / width, to.Height / height));
        double x = (from.Left + from.Right) / 2;
        double y = (from.Bottom + from.Top) / 2;
        double[] matrix =
        [
            scale * a, scale * b, scale * c, scale * d,
            ((to.Left + to.Right) / 2) - (scale * ((a * x) + (c * y))),
            ((to.Bottom + to.Top) / 2) - (scale * ((b * x) + (d * y))),
        ];

        if (matrix.Select(Round).SequenceEqual([1d, 0d, 0d, 1d, 0d, 0d]))
            return string.Empty;

        return string.Join(" ", matrix.Select(value => Round(value).ToString("0.#####", CultureInfo.InvariantCulture))) + " cm\n";

        // Rounded to what the matrix is written with, and never to minus zero.
        static double Round(double value) => Math.Round(value, 5) is double rounded && rounded != 0 ? rounded : 0;
    }

    /// <summary>A page's visible box — its crop box, or else its media box — or null when it is not four numbers enclosing something.</summary>
    private static (double Left, double Bottom, double Right, double Top, double Width, double Height)? VisibleBox(SourcePage page)
    {
        PdfValue given = page.Dictionary.TryGetValue(CropBox, out PdfValue crop) ? crop : page.Dictionary[PdfNames.MediaBox];

        if (page.Source.Resolve(given) is not { Kind: PdfValueKind.Array } array || array.AsArray().Count != 4)
            return null;

        double[] corners = new double[4];

        for (int index = 0; index < 4; index++)
        {
            PdfValue corner = page.Source.Resolve(array.AsArray()[index]);

            if (corner.Kind is not (PdfValueKind.Integer or PdfValueKind.Real))
                return null;

            corners[index] = corner.Kind == PdfValueKind.Integer ? corner.AsInteger() : corner.AsReal();
        }

        double left = Math.Min(corners[0], corners[2]);
        double right = Math.Max(corners[0], corners[2]);
        double bottom = Math.Min(corners[1], corners[3]);
        double top = Math.Max(corners[1], corners[3]);

        return right - left > 0 && top - bottom > 0 ? (left, bottom, right, top, right - left, top - bottom) : null;
    }

    /// <summary>Whether <paramref name="source"/> says it is tagged: its catalog's <c>/MarkInfo</c> has <c>/Marked true</c>.</summary>
    private static bool IsMarked(PdfSource source) =>
        source.Catalog.TryGetValue(MarkInfo, out PdfValue given) && source.Resolve(given) is { Kind: PdfValueKind.Dictionary } info
        && info.AsDictionary().TryGetValue(Marked, out PdfValue marked) && source.Resolve(marked) is { Kind: PdfValueKind.Boolean } flag && flag.AsBoolean();

    /// <summary>How far a page is turned clockwise as it is shown: a multiple of 90 degrees, or none.</summary>
    private static int Rotation(SourcePage page) =>
        page.Dictionary.TryGetValue(Rotate, out PdfValue given) && page.Source.Resolve(given) is { Kind: PdfValueKind.Integer } rotate && rotate.AsInteger() % 90 == 0
            ? (int)(rotate.AsInteger() % 360)
            : 0;

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

    /// <summary>
    /// The optional content of every file the pages come from, merged (8.11.4): content in a group the file does not
    /// list is shown by viewers, so a layer its own file hid would appear. The groups of every file are listed, and
    /// each file's hidden groups hidden, whatever pages are kept, since the properties name groups and not pages;
    /// the first file's default configuration is kept otherwise, each other file's order and radio groups joined to it.
    /// </summary>
    private static PdfDictionary? OptionalContent(ObjectCopier copier, List<PdfSource> sources)
    {
        PdfDictionary? merged = null;
        PdfDictionary configuration = new PdfDictionary();
        PdfArray groups = new PdfArray();
        PdfArray hidden = new PdfArray();
        Dictionary<PdfName, PdfArray> joined = Joined.ToDictionary(key => key, _ => new PdfArray());

        foreach (PdfSource source in sources)
        {
            if (!source.Catalog.TryGetValue(OCProperties, out PdfValue given) || source.Resolve(given) is not { Kind: PdfValueKind.Dictionary } found)
                continue;

            PdfDictionary properties = found.AsDictionary();
            PdfDictionary shown = source.Resolve(properties.TryGetValue(PdfNames.D, out PdfValue d) ? d : PdfValue.Null) is { Kind: PdfValueKind.Dictionary } own
                ? own.AsDictionary()
                : new PdfDictionary();
            List<PdfValue> listed = Items(source, properties, OCGs);

            if (merged is null)
            {
                merged = copier.CopyDictionary(source, properties, OCGs, PdfNames.D);
                configuration = copier.CopyDictionary(source, shown, [BaseState, On, Off, .. Joined]);
            }

            foreach (PdfValue group in listed)
                groups.Add(copier.Copy(source, group));

            // A configuration that starts with every group hidden shows only those it names as on.
            if (source.Resolve(shown.TryGetValue(BaseState, out PdfValue state) ? state : PdfValue.Null) is { Kind: PdfValueKind.Name } name && name.AsName().Equals(Off))
            {
                HashSet<int> on = [.. Items(source, shown, On).Where(group => group.Kind == PdfValueKind.Reference).Select(group => group.AsReference().ObjectNumber)];

                foreach (PdfValue group in listed.Where(group => group.Kind == PdfValueKind.Reference && !on.Contains(group.AsReference().ObjectNumber)))
                    hidden.Add(copier.Copy(source, group));
            }
            else
            {
                foreach (PdfValue group in Items(source, shown, Off))
                    hidden.Add(copier.Copy(source, group));
            }

            foreach (PdfName key in Joined)
            {
                foreach (PdfValue item in Items(source, shown, key))
                    joined[key].Add(copier.Copy(source, item));
            }
        }

        if (merged is null)
            return null;

        merged[OCGs] = groups;
        configuration[Off] = hidden;

        foreach (KeyValuePair<PdfName, PdfArray> entry in joined.Where(entry => entry.Value.Count > 0))
            configuration[entry.Key] = entry.Value;

        merged[PdfNames.D] = configuration;
        return merged;
    }

    /// <summary>The items of the array <paramref name="dictionary"/> holds as <paramref name="key"/>, or none.</summary>
    private static List<PdfValue> Items(PdfSource source, PdfDictionary dictionary, PdfName key) =>
        dictionary.TryGetValue(key, out PdfValue value) && source.Resolve(value) is { Kind: PdfValueKind.Array } array
            ? array.AsArray().Cast<PdfValue>().ToList()
            : [];

    /// <summary>
    /// The interactive form of every file the pages come from, merged: the first file's fields all while its pages all
    /// are, and of every file otherwise the fields with a widget on a page kept; a field named as one already listed
    /// renamed, as qpdf renames it, with "+1", "+2" and so on; and the resources the fields' appearances are made from
    /// joined, a name already given keeping what it named first. The rest of the form is the first form's.
    /// </summary>
    private static PdfDictionary? InteractiveForm(ObjectCopier copier, PdfSource first, bool whole, List<PdfSource> sources, IReadOnlyList<PageEntry> pages)
    {
        HashSet<(PdfSource, int)> kept = [];

        foreach (SourcePage page in pages.Select(entry => entry.Page))
        {
            foreach (PdfValue annotation in Items(page.Source, page.Dictionary, PdfNames.Annots).Where(annotation => annotation.Kind == PdfValueKind.Reference))
                kept.Add((page.Source, annotation.AsReference().ObjectNumber));
        }

        PdfDictionary? form = null;
        PdfArray fields = new PdfArray();
        PdfDictionary resources = new PdfDictionary();
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        foreach (PdfSource source in sources)
        {
            if (!source.Catalog.TryGetValue(AcroForm, out PdfValue given) || source.Resolve(given) is not { Kind: PdfValueKind.Dictionary } found)
                continue;

            PdfDictionary acroForm = found.AsDictionary();
            bool all = whole && ReferenceEquals(source, first);
            form ??= copier.CopyDictionary(source, acroForm, Fields, DR);

            foreach (PdfValue field in Items(source, acroForm, Fields).Where(field => field.Kind == PdfValueKind.Reference))
            {
                if (!all && !OnPageKept(source, field, []))
                    continue;

                if (source.Resolve(field) is { Kind: PdfValueKind.Dictionary } dictionary && dictionary.AsDictionary().TryGetValue(T, out PdfValue title)
                    && source.Resolve(title) is { Kind: PdfValueKind.String } text && !names.Add(Convert.ToBase64String(text.AsString().Bytes.ToArray())))
                {
                    byte[] name = text.AsString().Bytes.ToArray();
                    bool wide = name.Length >= 2 && name[0] == 0xFE && name[1] == 0xFF;
                    byte[] renamed = name;

                    for (int copy = 1; !names.Add(Convert.ToBase64String(renamed)); copy++)
                    {
                        string suffix = "+" + copy.ToString(CultureInfo.InvariantCulture);
                        renamed = [.. name, .. wide ? Encoding.BigEndianUnicode.GetBytes(suffix) : Encoding.ASCII.GetBytes(suffix)];
                    }

                    copier.Amend(source, field.AsReference().ObjectNumber, T, new PdfString(renamed));
                }

                fields.Add(copier.Copy(source, field));
            }

            if (acroForm.TryGetValue(DR, out PdfValue held) && source.Resolve(held) is { Kind: PdfValueKind.Dictionary } dr)
            {
                foreach (KeyValuePair<PdfName, PdfValue> category in dr.AsDictionary())
                {
                    if (source.Resolve(category.Value) is not { Kind: PdfValueKind.Dictionary } named)
                    {
                        if (!resources.ContainsKey(category.Key))
                            resources[category.Key] = copier.Copy(source, category.Value);

                        continue;
                    }

                    PdfDictionary into = resources.TryGetValue(category.Key, out PdfValue before) && before.Kind == PdfValueKind.Dictionary
                        ? before.AsDictionary()
                        : new PdfDictionary();
                    resources[category.Key] = into;

                    foreach (KeyValuePair<PdfName, PdfValue> entry in named.AsDictionary().Where(entry => !into.ContainsKey(entry.Key)))
                        into[entry.Key] = copier.Copy(source, entry.Value);
                }
            }
        }

        // A form none of whose fields are kept is left out, unless it is the first file's, kept whole as it was.
        if (form is null || (fields.Count == 0 && !(whole && first.Catalog.ContainsKey(AcroForm))))
            return null;

        form[Fields] = fields;

        if (resources.Count > 0)
            form[DR] = resources;

        return form;

        // Whether the field, or a field or widget beneath it, is listed among the annotations of a page kept.
        bool OnPageKept(PdfSource source, PdfValue field, HashSet<int> visited)
        {
            if (field.Kind != PdfValueKind.Reference || !visited.Add(field.AsReference().ObjectNumber))
                return false;

            return kept.Contains((source, field.AsReference().ObjectNumber))
                || (source.Resolve(field) is { Kind: PdfValueKind.Dictionary } node && Items(source, node.AsDictionary(), PdfNames.Kids).Any(kid => OnPageKept(source, kid, visited)));
        }
    }

    private static void CopyDocument(
        PdfDocumentWriter writer,
        ObjectCopier copier,
        PdfSource first,
        bool whole,
        SaveSettings settings,
        List<PdfSource> sources,
        IReadOnlyList<PageEntry> pages)
    {
        PdfDictionary catalog = first.Catalog;

        foreach (KeyValuePair<PdfName, PdfValue> entry in catalog)
        {
            if (entry.Key.Equals(PdfNames.Type) || entry.Key.Equals(PdfNames.Pages) || entry.Key.Equals(PdfNames.Names) || entry.Key.Equals(Associated)
                || entry.Key.Equals(OCProperties) || entry.Key.Equals(AcroForm) || (settings.LiftRestrictions && entry.Key.Equals(Perms)))
                continue;

            if (whole || Array.IndexOf(Always, entry.Key) >= 0)
                writer.Catalog[entry.Key] = copier.Copy(first, entry.Value);
        }

        if (OptionalContent(copier, sources) is { } optional)
            writer.Catalog[OCProperties] = optional;

        if (InteractiveForm(copier, first, whole, sources, pages) is { } form)
            writer.Catalog[AcroForm] = form;

        // Attached files belong to no page, so they stay whatever pages are kept; the other name trees point at pages.
        PdfDictionary? names = catalog.TryGetValue(PdfNames.Names, out PdfValue given) && first.Resolve(given) is { Kind: PdfValueKind.Dictionary } found
            ? found.AsDictionary()
            : null;

        PdfDictionary written = whole && names is not null ? copier.CopyDictionary(first, names, EmbeddedFiles) : new PdfDictionary();
        PdfNameTree files = new PdfNameTree();
        HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);

        if (names is not null && names.TryGetValue(EmbeddedFiles, out PdfValue tree))
        {
            foreach ((PdfString key, PdfValue value) in Assembly.EmbeddedFiles.Entries(first, tree))
            {
                if (listed.Add(Convert.ToBase64String(key.Bytes.ToArray())))
                    files.Add(key, copier.Copy(first, value));
            }
        }

        PdfArray associated = catalog.TryGetValue(Associated, out PdfValue af) && first.Resolve(af) is { Kind: PdfValueKind.Array } array
            ? copier.Copy(first, array).AsArray()
            : new PdfArray();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (FileAttachment attachment in settings.Attachments)
        {
            PdfReference specification = Assembly.EmbeddedFiles.Write(writer.File, attachment, now);
            string name = attachment.Name;

            // Two attachments of one name are listed apart, as a reader lists them.
            for (int copy = 2; !listed.Add(Convert.ToBase64String(PdfString.FromText(name).Bytes.ToArray())); copy++)
                name = $"{attachment.Name} ({copy})";

            files.Add(PdfString.FromText(name), specification);
            associated.Add(specification);
        }

        if (files.Count > 0)
            written[EmbeddedFiles] = files.Write(writer.File);

        if (written.Count > 0)
            writer.Catalog[PdfNames.Names] = written;

        if (associated.Count > 0)
            writer.Catalog[Associated] = associated;

        if (settings.Metadata.Count > 0)
        {
            byte[]? existing = catalog.TryGetValue(Metadata, out PdfValue metadata) && first.Stream(metadata) is { } stream ? first.Decode(stream) : null;

            writer.Catalog[Metadata] = writer.File.WriteStream(
                new PdfDictionary { [PdfNames.Type] = Metadata, [PdfNames.Subtype] = Xml },
                MetadataExtender.Extend(existing, settings.Metadata),
                PdfStreamCompression.None);
        }

        if (first.Trailer.TryGetValue(Info, out PdfValue info) && first.Resolve(info) is { Kind: PdfValueKind.Dictionary } dictionary)
            writer.InfoDictionary = copier.CopyDictionary(first, dictionary.AsDictionary());
    }
}
