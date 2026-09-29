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
    private static readonly XNamespace Extension = "http://www.aiim.org/pdfa/ns/extension/";
    private static readonly XNamespace Schema = "http://www.aiim.org/pdfa/ns/schema#";

    public static byte[] Extend(byte[]? existing, IEnumerable<string> additions)
    {
        XDocument packet = existing is null ? Empty() : Parse(existing);
        XElement rdf = packet.Descendants(Rdf + "RDF").FirstOrDefault()
            ?? throw new InvalidOperationException("The file's metadata has no rdf:RDF element to add to.");

        foreach (string addition in additions)
        {
            foreach (XElement element in Fragment(addition))
            {
                if (element.Name == Rdf + "Description")
                    Merge(rdf, element);
                else
                    rdf.Add(element);
            }
        }

        StringBuilder text = new StringBuilder("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        text.Append(packet.Root!.ToString(SaveOptions.None));
        text.Append("\n<?xpacket end=\"w\"?>");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>
    /// Adds <paramref name="description"/> to <paramref name="rdf"/> so that no property is said twice, which XMP
    /// readers, veraPDF among them, reject: a property already there is replaced by the one added, and the extension
    /// schemas PDF/A reads are added to the list already there, each schema once.
    /// </summary>
    private static void Merge(XElement rdf, XElement description)
    {
        List<XElement> existing = rdf.Elements(Rdf + "Description").ToList();
        HashSet<XElement> changed = [];
        bool moved = false;

        foreach (XElement property in description.Elements().ToList())
        {
            if (property.Name == Extension + "schemas" && existing.SelectMany(held => held.Elements(Extension + "schemas")).FirstOrDefault() is { } schemas)
            {
                XElement bag = schemas.Element(Rdf + "Bag") ?? new XElement(Rdf + "Bag");

                if (bag.Parent is null)
                    schemas.Add(bag);

                HashSet<string> known = new HashSet<string>(bag.Elements(Rdf + "li").Select(Namespace), StringComparer.Ordinal);

                foreach (XElement schema in property.Elements(Rdf + "Bag").Elements(Rdf + "li").Where(schema => known.Add(Namespace(schema))))
                    bag.Add(schema);

                // The schemas keep the prefixes they were given, which PDF/A requires of its own.
                XElement owner = schemas.Parent!;

                foreach (XAttribute declaration in description.Attributes().Where(attribute => attribute.IsNamespaceDeclaration && owner.Attribute(attribute.Name) is null))
                    owner.Add(new XAttribute(declaration));

                property.Remove();
                moved = true;
                continue;
            }

            foreach (XElement held in existing)
            {
                if (held.Elements(property.Name).Any() || held.Attribute(property.Name) is not null)
                {
                    held.Elements(property.Name).Remove();
                    held.Attribute(property.Name)?.Remove();
                    changed.Add(held);
                }
            }
        }

        // A property may be given as an attribute of its description, in XMP's shorthand.
        foreach (XAttribute attribute in description.Attributes().Where(IsProperty))
        {
            foreach (XElement held in existing.Where(held => held.Elements(attribute.Name).Any() || held.Attribute(attribute.Name) is not null))
            {
                held.Elements(attribute.Name).Remove();
                held.Attribute(attribute.Name)?.Remove();
                changed.Add(held);
            }
        }

        // A description all of whose properties were replaced says nothing any more.
        foreach (XElement held in changed.Where(held => !held.HasElements && !held.Attributes().Any(IsProperty)))
            held.Remove();

        // Unless all it said went to the schemas already there, the description is added, as it is if it says nothing.
        if (!moved || description.HasElements || description.Attributes().Any(IsProperty))
            rdf.Add(description);

        static string Namespace(XElement schema) => schema.Element(Schema + "namespaceURI")?.Value ?? string.Empty;

        static bool IsProperty(XAttribute attribute) => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Rdf && attribute.Name.Namespace != XNamespace.Xml;
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
