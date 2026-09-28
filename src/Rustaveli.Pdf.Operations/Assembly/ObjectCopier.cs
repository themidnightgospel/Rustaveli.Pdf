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
    private readonly Dictionary<(PdfSource Source, int Number), PdfReference?> _targets = [];
    private readonly Queue<(PdfSource Source, int Number, PdfReference Target)> _pending = new Queue<(PdfSource, int, PdfReference)>();

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

        return copied;
    }

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
