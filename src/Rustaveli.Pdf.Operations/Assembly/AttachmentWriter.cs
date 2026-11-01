using System.Security.Cryptography;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Writes a file attached to a PDF (ISO 32000-1, 7.11.4): its data as an embedded file stream, with its size, dates
/// and checksum, and the file specification that names it and says how it relates to the document.
/// </summary>
internal static class AttachmentWriter
{
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
    /// Writes <paramref name="attachment"/>: its data compressed, with its size, dates and checksum, and a file
    /// specification naming it; returns the specification.
    /// </summary>
    public static PdfReference Write(PdfFileWriter file, FileAttachment attachment, DateTimeOffset now)
    {
        PdfDictionary parameters = new PdfDictionary
        {
            [Size] = attachment.Content.Length,
            [ModDate] = PdfDate.Format(attachment.ModificationDate ?? now),
            [CheckSum] = new PdfString(MD5.HashData(attachment.Content), PdfStringForm.Hex),
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
}
