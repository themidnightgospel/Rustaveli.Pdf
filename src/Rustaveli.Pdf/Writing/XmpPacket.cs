using System.Globalization;
using System.Security;
using System.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// The XMP metadata of a document: what its information dictionary says, in the schemas PDF/A reads it from, and which
/// part and level of PDF/A the document claims.
/// </summary>
internal static class XmpPacket
{
    public static byte[] Build(PdfDocumentInfo info, int part, char conformance)
    {
        StringBuilder xmp = new StringBuilder();
        xmp.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        xmp.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n");
        xmp.Append(" <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        xmp.Append("  <rdf:Description rdf:about=\"\"");
        xmp.Append(" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"");
        xmp.Append(" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"");
        xmp.Append(" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\"");
        xmp.Append(" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");
        xmp.Append("   <pdfaid:part>").Append(part.ToString(CultureInfo.InvariantCulture)).Append("</pdfaid:part>\n");
        xmp.Append("   <pdfaid:conformance>").Append(conformance).Append("</pdfaid:conformance>\n");

        if (info.Title is { } title)
            xmp.Append("   <dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">").Append(Escape(title)).Append("</rdf:li></rdf:Alt></dc:title>\n");

        if (info.Author is { } author)
            xmp.Append("   <dc:creator><rdf:Seq><rdf:li>").Append(Escape(author)).Append("</rdf:li></rdf:Seq></dc:creator>\n");

        if (info.Subject is { } subject)
            xmp.Append("   <dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">").Append(Escape(subject)).Append("</rdf:li></rdf:Alt></dc:description>\n");

        Simple(xmp, "pdf:Keywords", info.Keywords);
        Simple(xmp, "pdf:Producer", info.Producer);
        Simple(xmp, "xmp:CreatorTool", info.Creator);
        Simple(xmp, "xmp:CreateDate", Date(info.CreationDate));
        Simple(xmp, "xmp:ModifyDate", Date(info.ModificationDate));

        xmp.Append("  </rdf:Description>\n");
        xmp.Append(" </rdf:RDF>\n");
        xmp.Append("</x:xmpmeta>\n");
        xmp.Append("<?xpacket end=\"r\"?>");

        return Encoding.UTF8.GetBytes(xmp.ToString());
    }

    private static void Simple(StringBuilder xmp, string property, string? value)
    {
        if (value is not null)
            xmp.Append("   <").Append(property).Append('>').Append(Escape(value)).Append("</").Append(property).Append(">\n");
    }

    /// <summary>A date as XMP writes it, to the second and with its offset, as the information dictionary has it.</summary>
    private static string? Date(DateTimeOffset? date) =>
        date?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;
}
