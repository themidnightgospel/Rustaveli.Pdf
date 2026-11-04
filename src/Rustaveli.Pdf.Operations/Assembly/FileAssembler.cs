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
/// <remarks>
/// One instance writes one file, so two saves of one <see cref="PdfFile"/> at once share nothing but the files read.
/// </remarks>
internal sealed class FileAssembler
{
    private static readonly PdfName Form = new PdfName("Form");
    private static readonly PdfName BBox = new PdfName("BBox");
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
    private static readonly PdfName EmbeddedFiles = new PdfName("EmbeddedFiles");
    private static readonly PdfName Associated = new PdfName("AF");

    /// <summary>
    /// The catalog entries written anew rather than copied. Declared after the names it holds, since static fields are
    /// initialized in the order they are written.
    /// </summary>
    private static readonly PdfName[] Rebuilt =
    [
        PdfNames.Type, PdfNames.Pages, PdfNames.Names, Associated, OCProperties, PdfNames.AcroForm,
    ];

    /// <summary>What a document says of itself whatever pages it keeps.</summary>
    private static readonly PdfName[] Always =
    [
        PdfNames.Metadata, PdfNames.Lang, PdfNames.OutputIntents, PdfNames.ViewerPreferences,
        new PdfName("PageLayout"), new PdfName("Version"), PdfNames.Extensions,
    ];

    private readonly IReadOnlyList<PageEntry> _pages;
    private readonly PdfSource _first;
    private readonly SaveSettings _settings;
    private readonly PdfDocumentWriter _writer;
    private readonly ObjectCopier _copier;

    /// <summary>
    /// Every file the pages come from, the first file first whether or not any of its pages are kept.
    /// </summary>
    private readonly List<PdfSource> _sources;

    /// <summary>
    /// Whether the first file's pages are all kept, in their order, at the start: its outline, names and structure
    /// point at its pages, so they are kept while those pages are.
    /// </summary>
    private readonly bool _whole;

    /// <summary>
    /// Each page laid on others, as the form it was written as: a page laid on many — a letterhead under every page —
    /// is written as a form once, and drawn wherever it is laid.
    /// </summary>
    private readonly Dictionary<(PdfSource, int), PdfReference> _forms = [];

    private FileAssembler(
        IReadOnlyList<PageEntry> pages,
        PdfSource first,
        SaveSettings settings,
        PdfDocumentWriter writer,
        PdfEncryption? encryption)
    {
        _pages = pages;
        _first = first;
        _settings = settings;
        _writer = writer;
        _copier = new ObjectCopier(writer.File, encryption);

        IEnumerable<PdfSource> drawn = pages
            .SelectMany(entry => entry.Over.Concat(entry.Beneath).Append(entry.Page))
            .Select(page => page.Source);
        _sources = new[] { first }.Concat(drawn).Distinct().ToList();

        _whole = first.Pages.Count <= pages.Count
            && first.Pages.Select((page, index) => ReferenceEquals(pages[index].Page, page)).All(same => same);
    }

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

    private static void Write(
        IReadOnlyList<PageEntry> pages,
        PdfSource first,
        Stream output,
        SaveSettings settings,
        PdfEncryption? encryption)
    {
        using PdfDocumentWriter writer = new PdfDocumentWriter(output, new PdfWriterOptions
        {
            CompressionLevel = CompressionLevel.Optimal,
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
            Encryption = encryption,
        });

        new FileAssembler(pages, first, settings, writer, encryption).Assemble();
    }

    /// <summary>
    /// Writes the pages, then what the document says of itself. The steps keep their order, which is the order the
    /// objects of the file are numbered in.
    /// </summary>
    private void Assemble()
    {
        List<(PdfReference Page, PdfReference Parent)> placed = _pages.Select(_ => _writer.AddPage()).ToList();

        // Every page of every file goes to the first page it became, or nowhere; the rest of their trees go nowhere.
        foreach (PdfSource source in _sources)
        {
            foreach (int node in source.PageTree)
                _copier.Redirect(source, node, null);

            if (source.Trailer.TryGetValue(PdfNames.Root, out PdfValue root) && root.Kind == PdfValueKind.Reference)
                _copier.Redirect(source, root.AsReference().ObjectNumber, null);
        }

        // A page kept twice is referred to as the first it became.
        HashSet<(PdfSource, int)> assigned = [];
        bool[] again = new bool[_pages.Count];

        for (int index = 0; index < _pages.Count; index++)
        {
            SourcePage page = _pages[index].Page;
            again[index] = !assigned.Add((page.Source, page.ObjectNumber));

            if (!again[index])
                _copier.Redirect(page.Source, page.ObjectNumber, placed[index].Page);
        }

        // Only the first file's named destinations are kept, and only then: every other file's links name destinations
        // of its own, which the names kept would take elsewhere or nowhere, so they are copied as the places they name.
        foreach (PdfSource source in _pages.Select(entry => entry.Page.Source).Distinct())
        {
            if (!KeptWhole(source))
                _copier.ResolveNamedDestinations(source);
        }

        for (int index = 0; index < _pages.Count; index++)
            WritePage(_pages[index], placed[index], again[index]);

        CopyDocument();
        _copier.Flush();
        _writer.Finish();
    }

    /// <summary>
    /// Whether <paramref name="source"/> is the first file with its pages all kept, in their order, at the start: then
    /// what points at its pages — its outline, named destinations, structure and form fields — is kept whole.
    /// </summary>
    private bool KeptWhole(PdfSource source) => _whole && ReferenceEquals(source, _first);

    private void WritePage(PageEntry entry, (PdfReference Page, PdfReference Parent) placed, bool again)
    {
        SourcePage page = entry.Page;
        PdfSource source = page.Source;

        // The structure tree knows a page kept twice only as the first it became, whose place in it the copy must not claim.
        PdfName[] leaving =
            KeptWhole(source) && !again ? [PdfNames.Parent] : [PdfNames.Parent, PdfNames.StructParents];
        PdfDictionary copied = _copier.CopyDictionary(source, page.Dictionary, leaving);
        copied[PdfNames.Parent] = placed.Parent;

        if (again && source.Resolve(page.Dictionary, PdfNames.Annots) is { Kind: PdfValueKind.Array } annotations)
            copied[PdfNames.Annots] = CopyAnnotations(source, annotations.AsArray(), placed.Page);

        if (entry.Beneath.Count > 0 || entry.Over.Count > 0)
            Layer(entry, copied);

        _writer.File.Write(placed.Page, copied);
    }

    /// <summary>
    /// The annotations of a page kept again, each written anew for <paramref name="page"/>: an annotation belongs to
    /// one page, which its <c>/P</c> names, and one place in the structure, which the first copy keeps.
    /// </summary>
    private PdfArray CopyAnnotations(PdfSource source, PdfArray annotations, PdfReference page)
    {
        PdfArray copies = new PdfArray(annotations.Count);

        foreach (PdfValue item in annotations)
        {
            if (source.Resolve(item) is { Kind: PdfValueKind.Dictionary } annotation)
            {
                PdfDictionary copy =
                    _copier.CopyDictionary(source, annotation.AsDictionary(), PdfNames.StructParent, PdfNames.P);
                copy[PdfNames.P] = page;
                copies.Add(_writer.File.Write(copy));
            }
            else
            {
                copies.Add(_copier.Copy(source, item));
            }
        }

        return copies;
    }

    /// <summary>
    /// Draws the pages laid beneath and over a page around its own content, each as a form of its own, the page's
    /// content kept in a saved state so it cannot disturb what is drawn over it.
    /// </summary>
    private void Layer(PageEntry entry, PdfDictionary copied)
    {
        PdfSource source = entry.Page.Source;
        PdfDictionary own = entry.Page.Dictionary;

        // Built from what the file holds, not from the copy: the copy's references number objects of the new file.
        PdfDictionary? held = source.Resolve(own, PdfNames.Resources) is { Kind: PdfValueKind.Dictionary } found
            ? found.AsDictionary()
            : null;

        PdfDictionary resources = held is null
            ? new PdfDictionary()
            : _copier.CopyDictionary(source, held, PdfNames.XObject);
        PdfDictionary xObjects =
            held is not null && source.Resolve(held, PdfNames.XObject) is { Kind: PdfValueKind.Dictionary } named
                ? _copier.CopyDictionary(source, named.AsDictionary())
                : new PdfDictionary();

        StringBuilder before = new StringBuilder();
        StringBuilder after = new StringBuilder("\nQ\n");
        int count = 0;

        // On a tagged page everything drawn is either in the structure or marked as not being content: what is laid
        // on it is neither the page's text nor any part of its structure, so it is an artifact.
        bool artifact = own.ContainsKey(PdfNames.StructParents) || IsMarked(source);

        foreach ((List<SourcePage> layers, StringBuilder content) in new[] { (entry.Beneath, before), (entry.Over, after) })
        {
            foreach (SourcePage layer in layers)
            {
                PdfName name;
                do
                {
                    name = new PdfName("Layer" + count++);
                }
                while (xObjects.ContainsKey(name));

                if (!_forms.TryGetValue((layer.Source, layer.ObjectNumber), out PdfReference form))
                {
                    form = FormOf(layer);
                    _forms[(layer.Source, layer.ObjectNumber)] = form;
                }

                xObjects[name] = form;
                content.Append(artifact ? "/Artifact BMC\nq\n" : "q\n")
                    .Append(Placement(layer, entry.Page))
                    .Append(Encoding.ASCII.GetString(name.Encoded.ToArray()))
                    .Append(artifact ? " Do\nQ\nEMC\n" : " Do\nQ\n");
            }
        }

        before.Append("q\n");
        resources[PdfNames.XObject] = xObjects;
        copied[PdfNames.Resources] = resources;

        PdfArray contents = new PdfArray
        {
            _writer.File.WriteStream(new PdfDictionary(), Encoding.ASCII.GetBytes(before.ToString())),
        };

        foreach (PdfValue stream in ContentStreams(entry.Page))
            contents.Add(_copier.Copy(source, stream));

        contents.Add(_writer.File.WriteStream(new PdfDictionary(), Encoding.ASCII.GetBytes(after.ToString())));
        copied[PdfNames.Contents] = contents;
    }

    /// <summary>The streams of <paramref name="page"/>'s <c>/Contents</c>: one, many, or none.</summary>
    private static IEnumerable<PdfValue> ContentStreams(SourcePage page)
    {
        if (!page.Dictionary.TryGetValue(PdfNames.Contents, out PdfValue contents))
            return [];

        return page.Source.Resolve(contents) is { Kind: PdfValueKind.Array } array ? array.AsArray() : [contents];
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
        PdfValue given = VisibleBoxEntry(page);

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

    /// <summary>The entry that gives a page's visible box: its crop box, or else its media box.</summary>
    private static PdfValue VisibleBoxEntry(SourcePage page) =>
        page.Dictionary.TryGetValue(PdfNames.CropBox, out PdfValue crop) ? crop : page.Dictionary[PdfNames.MediaBox];

    /// <summary>Whether <paramref name="source"/> says it is tagged: its catalog's <c>/MarkInfo</c> has <c>/Marked true</c>.</summary>
    private static bool IsMarked(PdfSource source) =>
        source.Resolve(source.Catalog, PdfNames.MarkInfo) is { Kind: PdfValueKind.Dictionary } info
        && source.Resolve(info.AsDictionary(), PdfNames.Marked) is { Kind: PdfValueKind.Boolean } flag
        && flag.AsBoolean();

    /// <summary>How far a page is turned clockwise as it is shown: a multiple of 90 degrees, or none.</summary>
    private static int Rotation(SourcePage page) =>
        page.Source.Resolve(page.Dictionary, PdfNames.Rotate) is { Kind: PdfValueKind.Integer } rotate
            && rotate.AsInteger() % 90 == 0
            ? (int)(rotate.AsInteger() % 360)
            : 0;

    /// <summary>A page as a form: its content, its resources, and its visible box as the form's bounds.</summary>
    private PdfReference FormOf(SourcePage page)
    {
        PdfSource source = page.Source;
        PdfDictionary form = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.XObject,
            [PdfNames.Subtype] = Form,
            [BBox] = _copier.Copy(source, source.Resolve(VisibleBoxEntry(page))),
            [PdfNames.Resources] = page.Dictionary.TryGetValue(PdfNames.Resources, out PdfValue resources)
                ? _copier.Copy(source, resources)
                : new PdfDictionary(),
        };

        using MemoryStream content = new MemoryStream();

        foreach (PdfValue stream in ContentStreams(page))
        {
            if (source.Stream(stream) is { } found)
            {
                byte[] data = source.Decode(found);
                content.Write(data, 0, data.Length);
                content.WriteByte((byte)'\n');
            }
        }

        return _writer.File.WriteStream(form, content.ToArray());
    }

    /// <summary>
    /// The optional content of every file the pages come from, merged (8.11.4): content in a group the file does not
    /// list is shown by viewers, so a layer its own file hid would appear. The groups of every file are listed, and
    /// each file's hidden groups hidden, whatever pages are kept, since the properties name groups and not pages;
    /// the first file's default configuration is kept otherwise, each other file's order and radio groups joined to it.
    /// </summary>
    private PdfDictionary? OptionalContent()
    {
        PdfDictionary? merged = null;
        PdfDictionary configuration = new PdfDictionary();
        PdfArray groups = new PdfArray();
        PdfArray hidden = new PdfArray();
        Dictionary<PdfName, PdfArray> joined = Joined.ToDictionary(key => key, _ => new PdfArray());

        foreach (PdfSource source in _sources)
        {
            if (source.Resolve(source.Catalog, OCProperties) is not { Kind: PdfValueKind.Dictionary } found)
                continue;

            PdfDictionary properties = found.AsDictionary();
            PdfDictionary shown = source.Resolve(properties, PdfNames.D) is { Kind: PdfValueKind.Dictionary } own
                ? own.AsDictionary()
                : new PdfDictionary();
            List<PdfValue> listed = Items(source, properties, OCGs);

            if (merged is null)
            {
                merged = _copier.CopyDictionary(source, properties, OCGs, PdfNames.D);
                configuration = _copier.CopyDictionary(source, shown, [BaseState, On, Off, .. Joined]);
            }

            foreach (PdfValue group in listed)
                groups.Add(_copier.Copy(source, group));

            // A configuration that starts with every group hidden shows only those it names as on.
            if (source.Resolve(shown, BaseState) is { Kind: PdfValueKind.Name } name && name.AsName().Equals(Off))
            {
                HashSet<int> on = [.. Items(source, shown, On).Where(group => group.Kind == PdfValueKind.Reference).Select(group => group.AsReference().ObjectNumber)];

                foreach (PdfValue group in listed.Where(group => group.Kind == PdfValueKind.Reference && !on.Contains(group.AsReference().ObjectNumber)))
                    hidden.Add(_copier.Copy(source, group));
            }
            else
            {
                foreach (PdfValue group in Items(source, shown, Off))
                    hidden.Add(_copier.Copy(source, group));
            }

            foreach (PdfName key in Joined)
            {
                foreach (PdfValue item in Items(source, shown, key))
                    joined[key].Add(_copier.Copy(source, item));
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
        source.Resolve(dictionary, key) is { Kind: PdfValueKind.Array } array ? array.AsArray().ToList() : [];

    /// <summary>
    /// The interactive form of every file the pages come from, merged: the first file's fields all while its pages all
    /// are, and of every file otherwise the fields with a widget on a page kept; a field named as one already listed
    /// renamed, as qpdf renames it, with "+1", "+2" and so on; and the resources the fields' appearances are made from
    /// joined, a name already given keeping what it named first. The rest of the form is the first form's.
    /// </summary>
    private PdfDictionary? InteractiveForm()
    {
        HashSet<(PdfSource, int)> kept = [];

        foreach (SourcePage page in _pages.Select(entry => entry.Page))
        {
            foreach (PdfValue annotation in Items(page.Source, page.Dictionary, PdfNames.Annots).Where(annotation => annotation.Kind == PdfValueKind.Reference))
                kept.Add((page.Source, annotation.AsReference().ObjectNumber));
        }

        PdfDictionary? form = null;
        PdfArray fields = new PdfArray();
        PdfDictionary resources = new PdfDictionary();
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        foreach (PdfSource source in _sources)
        {
            if (source.Resolve(source.Catalog, PdfNames.AcroForm) is not { Kind: PdfValueKind.Dictionary } found)
                continue;

            PdfDictionary acroForm = found.AsDictionary();
            form ??= _copier.CopyDictionary(source, acroForm, Fields, DR);

            foreach (PdfValue field in Items(source, acroForm, Fields).Where(field => field.Kind == PdfValueKind.Reference))
            {
                if (!KeptWhole(source) && !OnPageKept(source, field, []))
                    continue;

                RenameIfTaken(source, field, names);
                fields.Add(_copier.Copy(source, field));
            }

            JoinResources(source, acroForm, resources);
        }

        // A form none of whose fields are kept is left out, unless it is the first file's, kept whole as it was.
        if (form is null || (fields.Count == 0 && !(_whole && _first.Catalog.ContainsKey(PdfNames.AcroForm))))
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

    /// <summary>
    /// Has <paramref name="field"/> copied renamed when <paramref name="names"/> already holds its name, as qpdf
    /// renames it, with "+1", "+2" and so on; the name it is copied with joins them.
    /// </summary>
    private void RenameIfTaken(PdfSource source, PdfValue field, HashSet<string> names)
    {
        if (source.Resolve(field) is { Kind: PdfValueKind.Dictionary } dictionary
            && source.Resolve(dictionary.AsDictionary(), T) is { Kind: PdfValueKind.String } text
            && !names.Add(Convert.ToBase64String(text.AsString().Bytes.ToArray())))
        {
            byte[] name = text.AsString().Bytes.ToArray();
            bool wide = name.Length >= 2 && name[0] == 0xFE && name[1] == 0xFF;
            byte[] renamed = name;

            for (int copy = 1; !names.Add(Convert.ToBase64String(renamed)); copy++)
            {
                string suffix = "+" + copy.ToString(CultureInfo.InvariantCulture);
                byte[] added = wide ? Encoding.BigEndianUnicode.GetBytes(suffix) : Encoding.ASCII.GetBytes(suffix);
                renamed = [.. name, .. added];
            }

            _copier.Amend(source, field.AsReference().ObjectNumber, T, new PdfString(renamed));
        }
    }

    /// <summary>
    /// Joins to <paramref name="resources"/> those <paramref name="acroForm"/> makes its fields' appearances from, a
    /// name already given keeping what it named first.
    /// </summary>
    private void JoinResources(PdfSource source, PdfDictionary acroForm, PdfDictionary resources)
    {
        if (source.Resolve(acroForm, DR) is not { Kind: PdfValueKind.Dictionary } dr)
            return;

        foreach (KeyValuePair<PdfName, PdfValue> category in dr.AsDictionary())
        {
            if (source.Resolve(category.Value) is not { Kind: PdfValueKind.Dictionary } named)
            {
                if (!resources.ContainsKey(category.Key))
                    resources[category.Key] = _copier.Copy(source, category.Value);

                continue;
            }

            PdfDictionary into = resources.TryGetValue(category.Key, out PdfValue before)
                && before.Kind == PdfValueKind.Dictionary
                ? before.AsDictionary()
                : new PdfDictionary();
            resources[category.Key] = into;

            foreach (KeyValuePair<PdfName, PdfValue> entry in named.AsDictionary())
            {
                if (!into.ContainsKey(entry.Key))
                    into[entry.Key] = _copier.Copy(source, entry.Value);
            }
        }
    }

    /// <summary>
    /// Writes what the document says of itself: the first file's catalog, the optional content and forms of every file,
    /// the names and attachments, the metadata and the first file's information.
    /// </summary>
    private void CopyDocument()
    {
        foreach (KeyValuePair<PdfName, PdfValue> entry in _first.Catalog)
        {
            if (Array.IndexOf(Rebuilt, entry.Key) >= 0
                || (_settings.LiftRestrictions && entry.Key.Equals(PdfNames.Perms)))
            {
                continue;
            }

            if (_whole || Array.IndexOf(Always, entry.Key) >= 0)
                _writer.Catalog[entry.Key] = _copier.Copy(_first, entry.Value);
        }

        if (OptionalContent() is { } optional)
            _writer.Catalog[OCProperties] = optional;

        if (InteractiveForm() is { } form)
            _writer.Catalog[PdfNames.AcroForm] = form;

        CopyNames();
        ExtendMetadata();

        if (_first.Resolve(_first.Trailer, PdfNames.Info) is { Kind: PdfValueKind.Dictionary } dictionary)
            _writer.InfoDictionary = _copier.CopyDictionary(_first, dictionary.AsDictionary());
    }

    /// <summary>
    /// Writes the first file's name trees and associated files, with the attachments added: its attachments listed
    /// once each by name, and each added one under a name of its own.
    /// </summary>
    private void CopyNames()
    {
        PdfDictionary catalog = _first.Catalog;

        // Attached files belong to no page, so they stay whatever pages are kept; the other name trees point at pages.
        PdfDictionary? names = _first.Resolve(catalog, PdfNames.Names) is { Kind: PdfValueKind.Dictionary } found
            ? found.AsDictionary()
            : null;

        PdfDictionary written = _whole && names is not null
            ? _copier.CopyDictionary(_first, names, EmbeddedFiles)
            : new PdfDictionary();
        PdfNameTree files = new PdfNameTree();
        HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);

        if (names is not null && names.TryGetValue(EmbeddedFiles, out PdfValue tree))
        {
            foreach ((PdfString key, PdfValue value) in PdfNameTreeReader.Entries(_first, tree))
            {
                if (listed.Add(Convert.ToBase64String(key.Bytes.ToArray())))
                    files.Add(key, _copier.Copy(_first, value));
            }
        }

        PdfArray associated = _first.Resolve(catalog, Associated) is { Kind: PdfValueKind.Array } array
            ? _copier.Copy(_first, array).AsArray()
            : new PdfArray();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (FileAttachment attachment in _settings.Attachments)
        {
            PdfReference specification = AttachmentWriter.Write(_writer.File, attachment, now);
            string name = attachment.Name;

            // Two attachments of one name are listed apart, as a reader lists them.
            for (int copy = 2; !listed.Add(Convert.ToBase64String(PdfString.FromText(name).Bytes.ToArray())); copy++)
                name = $"{attachment.Name} ({copy})";

            files.Add(PdfString.FromText(name), specification);
            associated.Add(specification);
        }

        if (files.Count > 0)
            written[EmbeddedFiles] = files.Write(_writer.File);

        if (written.Count > 0)
            _writer.Catalog[PdfNames.Names] = written;

        if (associated.Count > 0)
            _writer.Catalog[Associated] = associated;
    }

    /// <summary>Adds the descriptions given to the first file's XMP metadata, or to a packet made anew.</summary>
    private void ExtendMetadata()
    {
        if (_settings.Metadata.Count == 0)
            return;

        byte[]? existing = _first.Catalog.TryGetValue(PdfNames.Metadata, out PdfValue metadata)
            && _first.Stream(metadata) is { } stream
            ? _first.Decode(stream)
            : null;

        XmpPacket.Write(_writer, MetadataExtender.Extend(existing, _settings.Metadata));
    }
}
