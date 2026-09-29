using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Copies objects from files being read into the file being written: each object once, however often it is reached,
/// renumbered, with its streams kept exactly as they were encoded.
/// </summary>
/// <remarks>
/// Some objects are not copied but redirected: a page to the page it became, or to nothing when it was left out, so
/// that copying an annotation does not pull in the page tree it hangs from, and with it every other page.
/// </remarks>
internal sealed class ObjectCopier(PdfFileWriter file)
{
    private static readonly PdfName Dest = new PdfName("Dest");

    private readonly Dictionary<(PdfSource Source, int Number), PdfReference?> _targets = [];
    private readonly Queue<(PdfSource Source, int Number, PdfReference Target)> _pending = new Queue<(PdfSource, int, PdfReference)>();
    private readonly Dictionary<PdfSource, Dictionary<string, PdfValue>> _named = [];

    /// <summary>Sends every reference to <paramref name="number"/> in <paramref name="source"/> to <paramref name="target"/>, or to null.</summary>
    public void Redirect(PdfSource source, int number, PdfReference? target) => _targets[(source, number)] = target;

    /// <summary>A copy of <paramref name="value"/> for the file being written; objects it refers to are copied by <see cref="Flush"/>.</summary>
    public PdfValue Copy(PdfSource source, PdfValue value)
    {
        switch (value.Kind)
        {
            case PdfValueKind.Reference:
                int number = value.AsReference().ObjectNumber;

                if (!_targets.TryGetValue((source, number), out PdfReference? target))
                {
                    target = file.Reserve();
                    _targets[(source, number)] = target;
                    _pending.Enqueue((source, number, target.Value));
                }

                return target is { } found ? found : PdfValue.Null;

            case PdfValueKind.Array:
                PdfArray array = value.AsArray();
                PdfArray copied = new PdfArray(array.Count);

                foreach (PdfValue item in array)
                    copied.Add(Copy(source, item));

                return copied;

            case PdfValueKind.Dictionary:
                return CopyDictionary(source, value.AsDictionary());

            default:
                return value;
        }
    }

    public PdfDictionary CopyDictionary(PdfSource source, PdfDictionary dictionary, params PdfName[] leaving)
    {
        PdfDictionary copied = new PdfDictionary(dictionary.Count);

        foreach (KeyValuePair<PdfName, PdfValue> entry in dictionary)
        {
            if (Array.IndexOf(leaving, entry.Key) < 0)
                copied[entry.Key] = Copy(source, entry.Value);
        }

        if (_named.TryGetValue(source, out Dictionary<string, PdfValue>? named))
        {
            // A link's /Dest, or a go-to action's /D; a remote go-to's names another file's destination, and is left be.
            PdfName? key = dictionary.ContainsKey(Dest) ? Dest
                : dictionary.TryGetValue(PdfNames.S, out PdfValue action) && source.Resolve(action) is { Kind: PdfValueKind.Name } kind && kind.AsName().Equals(PdfNames.GoTo) ? PdfNames.D
                : null;

            if (key is { } found && dictionary.TryGetValue(found, out PdfValue given)
                && Key(source.Resolve(given)) is { } name && named.TryGetValue(name, out PdfValue explicitly))
            {
                copied[found] = Copy(source, explicitly);
            }
        }

        return copied;
    }

    /// <summary>
    /// Has every named destination in <paramref name="source"/> — a link's, or a go-to action's — copied as the page and
    /// place it names, for a file whose names are not kept: the names of each file mean its own pages, and the file
    /// being written keeps at most the first file's.
    /// </summary>
    public void ResolveNamedDestinations(PdfSource source)
    {
        Dictionary<string, PdfValue> named = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        PdfDictionary catalog = source.Catalog;

        // Names are looked up by their bytes, whether the file gives them as strings (PDF 1.2) or as names (PDF 1.1).
        if (catalog.TryGetValue(PdfNames.Names, out PdfValue names) && source.Resolve(names) is { Kind: PdfValueKind.Dictionary } tree
            && tree.AsDictionary().TryGetValue(PdfNames.Dests, out PdfValue root))
        {
            foreach ((PdfString name, PdfValue value) in EmbeddedFiles.Entries(source, root))
                Add(Key(name)!, value);
        }

        if (catalog.TryGetValue(PdfNames.Dests, out PdfValue old) && source.Resolve(old) is { Kind: PdfValueKind.Dictionary } dests)
        {
            foreach (KeyValuePair<PdfName, PdfValue> entry in dests.AsDictionary())
                Add(Key(entry.Key)!, entry.Value);
        }

        _named[source] = named;

        void Add(string name, PdfValue value)
        {
            // A destination is an array, or a dictionary holding one as /D (12.3.2.3).
            PdfValue found = source.Resolve(value);

            if (found.Kind == PdfValueKind.Dictionary && found.AsDictionary().TryGetValue(PdfNames.D, out PdfValue inner))
                found = source.Resolve(inner);

            if (found.Kind == PdfValueKind.Array && !named.ContainsKey(name))
                named[name] = found;
        }
    }

    /// <summary>
    /// What a named destination is looked up by: a string read as the name of the same bytes would be, or null for
    /// what is neither a string nor a name.
    /// </summary>
    private static string? Key(PdfValue value) => value.Kind switch
    {
        PdfValueKind.String => PdfName.FromBytes(value.AsString().Bytes).Value,
        PdfValueKind.Name => value.AsName().Value,
        _ => null,
    };

    /// <summary>Writes every object reached so far, and every object those reach, until nothing is left to copy.</summary>
    public void Flush()
    {
        while (_pending.Count > 0)
        {
            (PdfSource source, int number, PdfReference target) = _pending.Dequeue();
            object read = source.GetObject(number);

            if (read is SourceStream stream)
            {
                PdfDictionary dictionary = CopyDictionary(source, stream.Dictionary, PdfNames.Length);

                // Data already encoded is kept as it is; data never compressed is compressed now.
                file.WriteStream(target, dictionary, stream.Data, dictionary.ContainsKey(PdfNames.Filter) ? PdfStreamCompression.None : PdfStreamCompression.Auto);
                continue;
            }

            PdfValue value = (PdfValue)read;

            // An object that is only a reference to another is written as what it refers to.
            if (value.Kind == PdfValueKind.Reference)
                value = source.Resolve(value);

            file.Write(target, Copy(source, value));
        }
    }
}
