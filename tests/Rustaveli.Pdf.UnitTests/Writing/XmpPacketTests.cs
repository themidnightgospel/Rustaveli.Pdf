using System.Text;
using System.Xml.Linq;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// A document's XMP metadata: what its information dictionary says, and the standards it claims.
/// </summary>
public class XmpPacketTests
{
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace PdfAId = "http://www.aiim.org/pdfa/ns/id/";
    private static readonly XNamespace PdfUAId = "http://www.aiim.org/pdfua/ns/id/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace Extension = "http://www.aiim.org/pdfa/ns/extension/";
    private static readonly XNamespace Schema = "http://www.aiim.org/pdfa/ns/schema#";

    private static string Packet(PdfDocumentInfo info, (int, char)? archive, int? accessibility) =>
        Encoding.UTF8.GetString(XmpPacket.Build(info, archive, accessibility));

    private static XElement Description(string packet)
    {
        // The packet's processing instructions wrap an ordinary XML document.
        XDocument document = XDocument.Parse(packet);
        return document.Descendants(Rdf + "Description").First();
    }

    [Fact]
    public void ThePacketIsWrappedForScanningAndStartsWithAByteOrderMark()
    {
        string packet = Packet(new PdfDocumentInfo(), (2, 'B'), null);

        Assert.StartsWith("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n", packet, StringComparison.Ordinal);
        Assert.EndsWith("<?xpacket end=\"r\"?>", packet, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArchiveClaimsItsPartAndLevel()
    {
        XElement description = Description(Packet(new PdfDocumentInfo(), (3, 'A'), null));

        Assert.Equal("3", description.Element(PdfAId + "part")!.Value);
        Assert.Equal("A", description.Element(PdfAId + "conformance")!.Value);
        Assert.Null(description.Element(PdfUAId + "part"));
    }

    [Fact]
    public void AnAccessibleDocumentClaimsItsPartWithoutDescribingTheSchemaOutsidePdfA()
    {
        string packet = Packet(new PdfDocumentInfo(), null, 1);
        XElement description = Description(packet);

        Assert.Equal("1", description.Element(PdfUAId + "part")!.Value);
        Assert.Null(description.Element(PdfAId + "part"));
        Assert.DoesNotContain("pdfaExtension", packet, StringComparison.Ordinal);
    }

    [Fact]
    public void UnderPdfAThePdfUASchemaIsDescribed()
    {
        XDocument document = XDocument.Parse(Packet(new PdfDocumentInfo(), (2, 'A'), 1));

        XElement schema = document.Descendants(Extension + "schemas").Single().Descendants(Rdf + "li").First();
        Assert.Equal("http://www.aiim.org/pdfua/ns/id/", schema.Element(Schema + "namespaceURI")!.Value);
        Assert.Equal("pdfuaid", schema.Element(Schema + "prefix")!.Value);
        Assert.Contains(schema.Descendants(), element => element.Name.LocalName == "name" && element.Value == "part");
    }

    [Fact]
    public void TheInformationIsRepeatedInTheSchemasStandardsReadItFrom()
    {
        PdfDocumentInfo info = new PdfDocumentInfo
        {
            Title = "Ledger <2026> & more",
            Author = "Accounts",
            Subject = "Year end",
            Keywords = "ledger, audit",
            Producer = "Rustaveli.Pdf",
            Creator = "Books",
            CreationDate = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(4)),
            ModificationDate = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.FromHours(-5)),
        };

        XElement description = Description(Packet(info, (2, 'U'), null));

        Assert.Equal("Ledger <2026> & more", description.Element(Dc + "title")!.Descendants(Rdf + "li").Single().Value);
        Assert.Equal("x-default", description.Element(Dc + "title")!.Descendants(Rdf + "li").Single().Attribute(XNamespace.Xml + "lang")!.Value);
        Assert.Equal("Accounts", description.Element(Dc + "creator")!.Element(Rdf + "Seq")!.Element(Rdf + "li")!.Value);
        Assert.Equal("Year end", description.Element(Dc + "description")!.Descendants(Rdf + "li").Single().Value);
        Assert.Equal("ledger, audit", description.Element(Pdf + "Keywords")!.Value);
        Assert.Equal("Rustaveli.Pdf", description.Element(Pdf + "Producer")!.Value);
        Assert.Equal("Books", description.Element(Xmp + "CreatorTool")!.Value);
        Assert.Equal("2026-01-02T03:04:05+04:00", description.Element(Xmp + "CreateDate")!.Value);
        Assert.Equal("2026-06-07T08:09:10-05:00", description.Element(Xmp + "ModifyDate")!.Value);
    }

    [Fact]
    public void EveryCharacterXmlCanHoldIsReadBackAsWritten()
    {
        // A parser turns a raw carriage return into a line feed; the metadata must still say what the information does.
        PdfDocumentInfo info = new PdfDocumentInfo
        {
            Title = "Q3\r\nReport",
            Author = "O'Brien \"Books\"",
            Subject = "Tab\there, line\nthere, return\ronly",
            Keywords = "emoji \U0001F600, �, ",
        };

        XElement description = Description(Packet(info, (2, 'B'), null));

        Assert.Equal("Q3\r\nReport", description.Element(Dc + "title")!.Descendants(Rdf + "li").Single().Value);
        Assert.Equal("O'Brien \"Books\"", description.Element(Dc + "creator")!.Descendants(Rdf + "li").Single().Value);
        Assert.Equal("Tab\there, line\nthere, return\ronly", description.Element(Dc + "description")!.Descendants(Rdf + "li").Single().Value);
        Assert.Equal("emoji \U0001F600, �, ", description.Element(Pdf + "Keywords")!.Value);
    }

    [Fact]
    public void ACharacterXmlCannotHoldIsRefusedNamingTheEntry()
    {
        // XML 1.0 has no way to write these, even escaped: the packet would not parse, and PDF/A needs it to. They are
        // listed here rather than as theory data, which would not carry a lone surrogate through intact.
        foreach (string value in new[] { "A\u0001B", "A\u001FB", "A￾B", "A￿", "A\uD800B", "A\uD800", "A\uDC00B" })
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Packet(new PdfDocumentInfo { Producer = value }, (2, 'B'), null));

            Assert.Contains("Producer", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WhatTheInformationLeavesOutTheMetadataLeavesOut()
    {
        XElement description = Description(Packet(new PdfDocumentInfo(), (2, 'B'), null));

        Assert.Equal(["part", "conformance"], description.Elements().Select(element => element.Name.LocalName));
    }

    [Fact]
    public void ThePacketIsTheCatalogsUncompressedMetadata()
    {
        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
        {
            writer.Info.Title = "Kept";
            writer.EndPage(writer.BeginPage(10, 10));
            XmpPacket.Write(writer, (2, 'B'), 1);
            writer.Finish();
        }

        PdfFileReader reader = new PdfFileReader(output.ToArray());
        ParsedStream metadata = Assert.IsType<ParsedStream>(reader.Resolve(reader.Catalog()["Metadata"]));

        Assert.Equal(new ParsedName("Metadata"), metadata.Dictionary["Type"]);
        Assert.Equal(new ParsedName("XML"), metadata.Dictionary["Subtype"]);
        Assert.False(metadata.Dictionary.ContainsKey("Filter"));

        string packet = Encoding.UTF8.GetString(metadata.Data);
        Assert.Contains("<pdfuaid:part>1</pdfuaid:part>", packet, StringComparison.Ordinal);
        Assert.Contains(">Kept</rdf:li>", packet, StringComparison.Ordinal);
    }
}
