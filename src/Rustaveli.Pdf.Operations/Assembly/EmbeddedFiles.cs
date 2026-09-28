using System.Security.Cryptography;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Writes a file's attachments (ISO 32000-1, 7.11.4): those it already had, and those added, in the name tree readers
/// list attachments from, and in the associated files PDF/A-3 reads their relationship from.
/// </summary>
internal static class EmbeddedFiles
{
    private static readonly PdfName Names = PdfNames.Names;
    private static readonly PdfName Kids = PdfNames.Kids;
    private static readonly PdfName Filespec = new PdfName("Filespec");
    private static readonly PdfName EmbeddedFile = new PdfName("EmbeddedFile");
    private static readonly PdfName F = new PdfName("F");
    private static readonly PdfName UF = new PdfName("UF");
    private static readonly PdfName EF = new PdfName("EF");
    private static readonly PdfName Desc = new PdfName("Desc");
    private static readonly PdfName AFRelationship = new PdfName("AFRelationship");
    private static readonly PdfName Params = new PdfName("Params");
    private static readonly PdfName Size = PdfNames.Size;
    private static readonly PdfName CreationDate = PdfNames.CreationDate;
    private static readonly PdfName ModDate = PdfNames.ModDate;
    private static readonly PdfName CheckSum = new PdfName("CheckSum");

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

            if (dictionary.TryGetValue(Names, out PdfValue pairs) && source.Resolve(pairs) is { Kind: PdfValueKind.Array } array)
            {
                PdfArray items = array.AsArray();

                for (int index = 0; index + 1 < items.Count; index += 2)
                {
                    if (source.Resolve(items[index]) is { Kind: PdfValueKind.String } key)
                        entries.Add((key.AsString(), items[index + 1]));
                }
            }

            if (dictionary.TryGetValue(Kids, out PdfValue kids) && source.Resolve(kids) is { Kind: PdfValueKind.Array } children)
            {
                foreach (PdfValue kid in children.AsArray())
                    Visit(kid, depth + 1);
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="attachment"/>: its data compressed, with its size, dates and checksum, and a file
    /// specification naming it; returns the specification.
    /// </summary>
    public static PdfReference Write(PdfFileWriter file, FileAttachment attachment, DateTimeOffset now)
    {
        PdfDictionary parameters = new PdfDictionary
        {
            [Size] = attachment.Content.Length,
            [ModDate] = PdfDate.Format(attachment.ModificationDate ?? now),
            [CheckSum] = new PdfString(Md5(attachment.Content), PdfStringForm.Hex),
        };

        if (attachment.CreationDate is { } created)
            parameters[CreationDate] = PdfDate.Format(created);

        PdfDictionary stream = new PdfDictionary { [PdfNames.Type] = EmbeddedFile, [Params] = parameters };

        if (attachment.MediaType is { Length: > 0 } type)
            stream[PdfNames.Subtype] = new PdfName(type);

        PdfReference data = file.WriteStream(stream, attachment.Content);
        PdfDictionary specification = new PdfDictionary
        {
            [PdfNames.Type] = Filespec,
            [F] = PdfString.FromText(attachment.Name),
            [UF] = PdfString.FromText(attachment.Name),
            [EF] = new PdfDictionary { [F] = data, [UF] = data },
            [AFRelationship] = new PdfName(attachment.Relationship.ToString()),
        };

        if (attachment.Description is { } description)
            specification[Desc] = PdfString.FromText(description);

        return file.Write(specification);
    }

    private static byte[] Md5(byte[] data)
    {
        using MD5 md5 = MD5.Create();
        return md5.ComputeHash(data);
    }
}
