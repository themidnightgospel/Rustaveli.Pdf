using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// Reads a name tree (ISO 32000-1, 7.9.6) of a file being read: the counterpart of <see cref="PdfNameTree"/>, which
/// writes one.
/// </summary>
internal static class PdfNameTreeReader
{
    /// <summary>
    /// The entries of the name tree at <paramref name="root"/> in <paramref name="source"/>, key and value, in order,
    /// each node visited once however the tree is drawn.
    /// </summary>
    public static List<(PdfString Key, PdfValue Value)> Entries(PdfSource source, PdfValue root)
    {
        List<(PdfString, PdfValue)> entries = [];
        HashSet<int> visited = [];
        Visit(root, 0);
        return entries;

        void Visit(PdfValue node, int depth)
        {
            if (depth > 32 || (node.Kind == PdfValueKind.Reference && !visited.Add(node.AsReference().ObjectNumber)))
                return;

            if (source.Resolve(node) is not { Kind: PdfValueKind.Dictionary } found)
                return;

            PdfDictionary dictionary = found.AsDictionary();

            if (source.Resolve(dictionary, PdfNames.Names) is { Kind: PdfValueKind.Array } array)
            {
                PdfArray items = array.AsArray();

                for (int index = 0; index + 1 < items.Count; index += 2)
                {
                    if (source.Resolve(items[index]) is { Kind: PdfValueKind.String } key)
                        entries.Add((key.AsString(), items[index + 1]));
                }
            }

            if (source.Resolve(dictionary, PdfNames.Kids) is { Kind: PdfValueKind.Array } children)
            {
                foreach (PdfValue kid in children.AsArray())
                    Visit(kid, depth + 1);
            }
        }
    }
}
