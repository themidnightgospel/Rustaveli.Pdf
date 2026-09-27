using System.Globalization;
using System.Security;
using System.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// The XMP metadata of a document: what its information dictionary says, in the schemas PDF/A and PDF/UA read it
/// from, and which parts and levels of those standards the document claims.
/// </summary>
internal static class XmpPacket
{
    private static readonly PdfName Metadata = new PdfName("Metadata");
    private static readonly PdfName Xml = new PdfName("XML");

    /// <summary>
    /// Writes the packet as the catalog's metadata stream, left uncompressed so tools that scan for it without reading
    /// the file find it.
    /// </summary>
    public static void Write(PdfDocumentWriter writer, (int Part, char Level)? archive, int? accessibility)
    {
        writer.Catalog[Metadata] = writer.File.WriteStream(
            new PdfDictionary { [PdfNames.Type] = Metadata, [PdfNames.Subtype] = Xml },
            Build(writer.Info, archive, accessibility),
            PdfStreamCompression.None);
    }

    public static byte[] Build(PdfDocumentInfo info, (int Part, char Level)? archive, int? accessibility)
    {
        StringBuilder xmp = new StringBuilder();
        xmp.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        xmp.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n");
        xmp.Append(" <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        xmp.Append("  <rdf:Description rdf:about=\"\"");
        xmp.Append(" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"");
        xmp.Append(" xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\"");
        xmp.Append(" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"");
        xmp.Append(" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\"");
        xmp.Append(" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");

        if (archive is (int part, char level))
        {
            xmp.Append("   <pdfaid:part>").Append(part.ToString(CultureInfo.InvariantCulture)).Append("</pdfaid:part>\n");
            xmp.Append("   <pdfaid:conformance>").Append(level).Append("</pdfaid:conformance>\n");
        }

        if (accessibility is { } accessible)
            xmp.Append("   <pdfuaid:part>").Append(accessible.ToString(CultureInfo.InvariantCulture)).Append("</pdfuaid:part>\n");

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

        // PDF/A admits only the schemas it predefines, and those a file describes itself; PDF/UA's is not among the first.
        if (archive is not null && accessibility is not null)
            xmp.Append(AccessibilitySchema);

        xmp.Append(" </rdf:RDF>\n");
        xmp.Append("</x:xmpmeta>\n");
        xmp.Append("<?xpacket end=\"r\"?>");

        return Encoding.UTF8.GetBytes(xmp.ToString());
    }

    /// <summary>The description of PDF/UA's identification schema, as PDF/A requires of a schema it does not define.</summary>
    private const string AccessibilitySchema =
        "  <rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\""
        + " xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\">\n"
        + "   <pdfaExtension:schemas>\n"
        + "    <rdf:Bag>\n"
        + "     <rdf:li rdf:parseType=\"Resource\">\n"
        + "      <pdfaSchema:schema>PDF/UA Universal Accessibility Schema</pdfaSchema:schema>\n"
        + "      <pdfaSchema:namespaceURI>http://www.aiim.org/pdfua/ns/id/</pdfaSchema:namespaceURI>\n"
        + "      <pdfaSchema:prefix>pdfuaid</pdfaSchema:prefix>\n"
        + "      <pdfaSchema:property>\n"
        + "       <rdf:Seq>\n"
        + "        <rdf:li rdf:parseType=\"Resource\">\n"
        + "         <pdfaProperty:name>part</pdfaProperty:name>\n"
        + "         <pdfaProperty:valueType>Integer</pdfaProperty:valueType>\n"
        + "         <pdfaProperty:category>internal</pdfaProperty:category>\n"
        + "         <pdfaProperty:description>Indicates, which part of ISO 14289 standard is followed</pdfaProperty:description>\n"
        + "        </rdf:li>\n"
        + "       </rdf:Seq>\n"
        + "      </pdfaSchema:property>\n"
        + "     </rdf:li>\n"
        + "    </rdf:Bag>\n"
        + "   </pdfaExtension:schemas>\n"
        + "  </rdf:Description>\n";

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
