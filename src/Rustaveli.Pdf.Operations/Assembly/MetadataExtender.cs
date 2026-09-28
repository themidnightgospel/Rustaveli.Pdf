using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Adds descriptions to a file's XMP metadata: each given <c>rdf:Description</c>, and anything else given, is added to
/// the packet's <c>rdf:RDF</c>, the packet made anew when the file had none.
/// </summary>
internal static class MetadataExtender
{
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace X = "adobe:ns:meta/";

    public static byte[] Extend(byte[]? existing, IEnumerable<string> additions)
    {
        XDocument packet = existing is null ? Empty() : Parse(existing);
        XElement rdf = packet.Descendants(Rdf + "RDF").FirstOrDefault()
            ?? throw new InvalidOperationException("The file's metadata has no rdf:RDF element to add to.");

        foreach (string addition in additions)
        {
            foreach (XElement element in Fragment(addition))
                rdf.Add(element);
        }

        StringBuilder text = new StringBuilder("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        text.Append(packet.Root!.ToString(SaveOptions.None));
        text.Append("\n<?xpacket end=\"w\"?>");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static XDocument Empty() =>
        new XDocument(new XElement(X + "xmpmeta", new XAttribute(XNamespace.Xmlns + "x", X), new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf))));

    private static XDocument Parse(byte[] data)
    {
        string text = Encoding.UTF8.GetString(data).TrimStart('﻿');

        try
        {
            return XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new UnreadableFileException("The file's metadata is not well-formed XML, so it cannot be added to.", exception);
        }
    }

    /// <summary>The elements of <paramref name="xml"/>, which may be several, the <c>rdf</c> prefix known without declaring it.</summary>
    private static IEnumerable<XElement> Fragment(string xml)
    {
        XmlNamespaceManager namespaces = new XmlNamespaceManager(new NameTable());
        namespaces.AddNamespace("rdf", Rdf.NamespaceName);
        XmlParserContext context = new XmlParserContext(null, namespaces, null, XmlSpace.None);
        List<XElement> elements = [];

        try
        {
            using XmlReader reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment }, context);
            reader.MoveToContent();

            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element)
                    elements.Add((XElement)XNode.ReadFrom(reader));
                else
                    reader.Read();
            }
        }
        catch (XmlException exception)
        {
            throw new ArgumentException("The metadata to add is not well-formed XML: " + exception.Message, nameof(xml), exception);
        }

        if (elements.Count == 0)
            throw new ArgumentException("The metadata to add holds no element.", nameof(xml));

        return elements;
    }
}
