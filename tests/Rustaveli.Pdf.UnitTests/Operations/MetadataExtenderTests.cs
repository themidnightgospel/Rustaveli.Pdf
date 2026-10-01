using System.Text;
using System.Xml.Linq;
using Rustaveli.Pdf.Operations.Assembly;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>Descriptions added to a file's XMP metadata, and metadata that cannot be added to.</summary>
public class MetadataExtenderTests
{
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Kind = "urn:kind";
    private static readonly XNamespace Extension = "http://www.aiim.org/pdfa/ns/extension/";

    private static XDocument Extended(byte[]? existing, params string[] additions) =>
        XDocument.Parse(Encoding.UTF8.GetString(MetadataExtender.Extend(existing, additions)));

    /// <summary>A description of the <c>urn:kind</c> namespace, <paramref name="rest"/> completing its start tag and content.</summary>
    private static string Described(string rest) => "<rdf:Description rdf:about=\"\" xmlns:k=\"urn:kind\" " + rest + "</rdf:Description>";

    [Fact]
    public void MetadataWithoutAnRdfElementCannotBeAddedTo()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => MetadataExtender.Extend(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"), [Described(">")]));

        Assert.Contains("rdf:RDF", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataThatIsNotXmlIsUnreadable() =>
        Assert.Throws<UnreadableFileException>(() => MetadataExtender.Extend(Encoding.UTF8.GetBytes("<x:xmpmeta"), [Described(">")]));

    [Fact]
    public void WhatIsAddedBesideADescriptionJoinsTheRdfAsItIs()
    {
        XDocument metadata = Extended(null, "<k:Note xmlns:k=\"urn:kind\">kept</k:Note>");

        XElement note = Assert.Single(metadata.Descendants(Rdf + "RDF").Elements());
        Assert.Equal(Kind + "Note", note.Name);
        Assert.Equal("kept", note.Value);
    }

    [Theory]
    [InlineData("k:Kind=\"a\">")]
    [InlineData("><k:Kind>a</k:Kind>")]
    public void APropertyGivenAsAnAttributeReplacesOneGivenEitherWay(string first)
    {
        XDocument metadata = Extended(null, Described(first), Described("k:Kind=\"b\">"));

        XElement description = Assert.Single(metadata.Descendants(Rdf + "Description"));
        Assert.Equal("b", (string?)description.Attribute(Kind + "Kind"));
        Assert.Empty(description.Elements());
    }

    [Theory]
    [InlineData("k:Kind=\"x\">", "x")]
    [InlineData("><k:Kind>x</k:Kind>", "x")]
    [InlineData(">", null)]
    public void ADescriptionWhoseSchemasJoinThoseThereIsStillAddedForWhatElseItSays(string rest, string? kind)
    {
        string schemas = "<pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
            + "<pdfaSchema:namespaceURI xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\">urn:kind</pdfaSchema:namespaceURI>"
            + "</rdf:li></rdf:Bag></pdfaExtension:schemas>";
        string declarations = "xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\" ";
        byte[] existing = MetadataExtender.Extend(null, ["<rdf:Description rdf:about=\"\" " + declarations + ">" + schemas + "</rdf:Description>"]);

        string addition = rest.StartsWith(">", StringComparison.Ordinal)
            ? Described(declarations + ">" + schemas + rest.Substring(1))
            : Described(declarations + rest + schemas);
        XDocument metadata = Extended(existing, addition);

        Assert.Single(metadata.Descendants(Extension + "schemas"));
        Assert.Equal(kind is null ? 1 : 2, metadata.Descendants(Rdf + "Description").Count());

        XElement added = metadata.Descendants(Rdf + "Description").Last();
        Assert.Equal(kind, (string?)added.Attribute(Kind + "Kind") ?? added.Element(Kind + "Kind")?.Value);
    }
}
