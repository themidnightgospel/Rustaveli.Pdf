using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfDocumentWriterTests
{
    private static PdfFileReader Write(Action<PdfDocumentWriter> build)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter document = new PdfDocumentWriter(output))
        {
            build(document);
            document.Finish();
        }

        return new PdfFileReader(output.ToArray());
    }

    private static Dictionary<string, object?> OnlyPage(PdfFileReader reader) =>
        reader.Dictionary(new ParsedReference(Assert.Single(reader.Pages()).Number, 0));

    private static List<Dictionary<string, object?>> Annotations(PdfFileReader reader) =>
        ((List<object?>)OnlyPage(reader)["Annots"]!).Select(reader.Dictionary).ToList();

    [Fact]
    public void WritesABlankPageWithItsBoxAndNoContentStream()
    {
        PdfFileReader reader = Write(document => document.EndPage(document.BeginPage(595.28, 841.89)));

        Dictionary<string, object?> catalog = reader.Catalog();
        Dictionary<string, object?> page = OnlyPage(reader);

        Assert.Equal(new ParsedName("Catalog"), catalog["Type"]);
        Assert.False(catalog.ContainsKey("Names"));
        Assert.Equal(new ParsedName("Page"), page["Type"]);
        Assert.Equal(new List<object?> { 0L, 0L, 595.28, 841.89 }, page["MediaBox"]);
        Assert.Empty((Dictionary<string, object?>)page["Resources"]!);
        Assert.False(page.ContainsKey("Contents"));
        Assert.False(page.ContainsKey("Annots"));
        Assert.False(reader.Trailer.ContainsKey("Info"));
    }

    [Fact]
    public void WritesThePagesContentAndResources()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(new PdfRectangle(0, 0, 200, 100));
            PdfName state = page.Resources.GetExtGStateName(document.GetOpacityState(0.5));
            page.Content.SetGraphicsState(state);
            page.Content.Rectangle(10, 10, 50, 20);
            page.Content.Fill();
            document.EndPage(page);
        });

        Dictionary<string, object?> page = OnlyPage(reader);
        ParsedStream contents = (ParsedStream)reader.Resolve(page["Contents"])!;
        Dictionary<string, object?> resources = (Dictionary<string, object?>)page["Resources"]!;

        Assert.Equal("/GS1 gs\n10 10 50 20 re\nf\n", Latin1.Text(PdfFileReader.Decode(contents)));
        Assert.Equal(new[] { "ExtGState" }, resources.Keys);
        Assert.Equal(0.5, reader.Dictionary(((Dictionary<string, object?>)resources["ExtGState"]!)["GS1"])["ca"]);
    }

    [Fact]
    public void AddsTheCallersPageEntries()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(10, 10);
            page.Entries[new PdfName("Rotate")] = 90;
            document.EndPage(page);
        });

        Assert.Equal(90L, OnlyPage(reader)["Rotate"]);
    }

    [Theory]
    [InlineData("Type")]
    [InlineData("Parent")]
    [InlineData("MediaBox")]
    [InlineData("Resources")]
    [InlineData("Contents")]
    [InlineData("Annots")]
    public void RefusesPageEntriesTheWriterOwns(string key)
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        PdfPage page = document.BeginPage(10, 10);
        page.Entries[new PdfName(key)] = 1;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.EndPage(page));

        Assert.Equal($"The writer sets the page's /{key} entry itself.", exception.Message);
    }

    [Fact]
    public void AddsTheCallersCatalogEntries()
    {
        PdfFileReader reader = Write(document =>
        {
            document.Catalog[new PdfName("PageMode")] = new PdfName("UseOutlines");
            document.EndPage(document.BeginPage(10, 10));
        });

        Assert.Equal(new ParsedName("UseOutlines"), reader.Catalog()["PageMode"]);
    }

    [Theory]
    [InlineData("Type")]
    [InlineData("Pages")]
    public void RefusesCatalogEntriesTheWriterOwns(string key)
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        document.Catalog[new PdfName(key)] = 1;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.Finish());

        Assert.Equal($"The writer sets the catalog's /{key} entry itself.", exception.Message);
    }

    [Fact]
    public void KeepsNameTreesTheCallerSuppliesAndAddsDestinationsBesideThem()
    {
        PdfName embedded = new PdfName("EmbeddedFiles");

        PdfFileReader alone = Write(document =>
        {
            document.Catalog[PdfNames.Names] = new PdfDictionary { [embedded] = new PdfDictionary() };
            document.EndPage(document.BeginPage(10, 10));
        });

        PdfFileReader together = Write(document =>
        {
            document.Catalog[PdfNames.Names] = new PdfDictionary { [embedded] = new PdfDictionary() };
            PdfPage page = document.BeginPage(10, 10);
            document.AddNamedDestination("here", page.Reference, 0, 10);
            document.EndPage(page);
        });

        Assert.Equal(["EmbeddedFiles"], alone.Dictionary(alone.Catalog()["Names"]).Keys);
        Assert.Equal(["Dests", "EmbeddedFiles"], together.Dictionary(together.Catalog()["Names"]).Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void AddsPagesItDoesNotDrawInTheirPlaceInTheTree()
    {
        PdfFileReader reader = Write(document =>
        {
            document.EndPage(document.BeginPage(10, 10));
            (PdfReference page, PdfReference parent) = document.AddPage();
            document.File.Write(page, new PdfDictionary
            {
                [PdfNames.Type] = PdfNames.Page,
                [PdfNames.Parent] = parent,
                [PdfNames.MediaBox] = new PdfArray { 0, 0, 20, 20 },
            });
            document.EndPage(document.BeginPage(30, 30));
        });

        List<object?> widths = reader.Pages()
            .Select(page => ((List<object?>)reader.Dictionary(new ParsedReference(page.Number, 0))["MediaBox"]!)[2])
            .ToList();

        Assert.Equal([10L, 20L, 30L], widths);
    }

    [Fact]
    public void WritesAGivenInformationDictionaryInPlaceOfItsOwn()
    {
        PdfFileReader reader = Write(document =>
        {
            document.Info.Title = "Ignored";
            document.InfoDictionary = new PdfDictionary { [new PdfName("Custom")] = 7 };
            document.EndPage(document.BeginPage(10, 10));
        });

        Dictionary<string, object?> info = reader.Dictionary(reader.Trailer["Info"]);
        Assert.Equal(["Custom"], info.Keys);
    }

    [Fact]
    public void OrdersPagesAsTheyWereBegunNotAsTheyWereEnded()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage first = document.BeginPage(100, 100);
            PdfPage second = document.BeginPage(200, 200);
            Assert.Equal(0, first.Index);
            Assert.Equal(1, second.Index);
            Assert.Equal(2, document.PageCount);

            document.EndPage(second);
            document.EndPage(first);
        });

        List<(int Number, int Depth)> pages = reader.Pages();
        Assert.Equal(new List<object?> { 0L, 0L, 100L, 100L }, reader.Dictionary(new ParsedReference(pages[0].Number, 0))["MediaBox"]);
        Assert.Equal(new List<object?> { 0L, 0L, 200L, 200L }, reader.Dictionary(new ParsedReference(pages[1].Number, 0))["MediaBox"]);
    }

    [Fact]
    public void WritesUriLinksWithoutABorder()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(100, 100);
            page.AddUriLink(new PdfRectangle(1, 2, 30, 40), "https://example.com/a b?q=ü&x=~!");
            document.EndPage(page);
        });

        Dictionary<string, object?> link = Assert.Single(Annotations(reader));
        Assert.Equal(new ParsedName("Annot"), link["Type"]);
        Assert.Equal(new ParsedName("Link"), link["Subtype"]);
        Assert.Equal(new List<object?> { 1L, 2L, 30L, 40L }, link["Rect"]);
        Assert.Equal(new List<object?> { 0L, 0L, 0L }, link["Border"]);
        Dictionary<string, object?> action = (Dictionary<string, object?>)link["A"]!;
        Assert.Equal(new ParsedName("URI"), action["S"]);
        Assert.Equal("https://example.com/a%20b?q=%C3%BC&x=~!", Latin1.Text((byte[])action["URI"]!));
    }

    [Fact]
    public void PercentEncodesControlCharactersInUris()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(100, 100);
            page.AddUriLink(new PdfRectangle(0, 0, 1, 1), "a\u007Fb\tc");
            document.EndPage(page);
        });

        Dictionary<string, object?> action = (Dictionary<string, object?>)Assert.Single(Annotations(reader))["A"]!;
        Assert.Equal("a%7Fb%09c", Latin1.Text((byte[])action["URI"]!));
    }

    [Fact]
    public void WritesLinksToNamedDestinations()
    {
        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(100, 100);
            page.AddDestinationLink(new PdfRectangle(0, 0, 10, 10), "chapter-2");
            document.EndPage(page);
        });

        Dictionary<string, object?> action = (Dictionary<string, object?>)Assert.Single(Annotations(reader))["A"]!;
        Assert.Equal(new ParsedName("GoTo"), action["S"]);
        Assert.Equal("chapter-2", Latin1.Text((byte[])action["D"]!));
    }

    [Fact]
    public void ListsAnnotationsTheCallerWrote()
    {
        PdfReference annotation = default;

        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(100, 100);
            annotation = document.File.Write(new PdfDictionary { [PdfNames.Type] = PdfNames.Annot });
            page.AddAnnotation(annotation);
            document.EndPage(page);
        });

        Assert.Equal(new List<object?> { new ParsedReference(annotation.ObjectNumber, 0) }, OnlyPage(reader)["Annots"]);
    }

    [Fact]
    public void RefusesInvalidLinkArguments()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        PdfPage page = document.BeginPage(10, 10);

        Assert.Equal("uri", Assert.Throws<ArgumentNullException>(() => page.AddUriLink(default, null!)).ParamName);
        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentNullException>(() => page.AddDestinationLink(default, null!)).ParamName);
        ArgumentException annotation = Assert.Throws<ArgumentException>(() => page.AddAnnotation(default));
        Assert.Equal("annotation", annotation.ParamName);
        Assert.StartsWith("The reference was never assigned an object number.", annotation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesNamedDestinationsIntoTheCatalogsNameTree()
    {
        PdfReference target = default;

        PdfFileReader reader = Write(document =>
        {
            PdfPage page = document.BeginPage(100, 100);
            target = page.Reference;
            Assert.True(document.AddNamedDestination("zeta", page.Reference, 10, 90.5));
            Assert.True(document.AddNamedDestination("alpha", page.Reference, 0, 100));
            Assert.False(document.AddNamedDestination("zeta", page.Reference, 50, 50));
            document.EndPage(page);
        });

        Dictionary<string, object?> names = (Dictionary<string, object?>)reader.Catalog()["Names"]!;
        List<(byte[] Key, object? Value, int Depth)> destinations = reader.NameTree(names["Dests"]);
        ParsedReference page = new ParsedReference(target.ObjectNumber, 0);

        Assert.Equal(new[] { "alpha", "zeta" }, destinations.Select(entry => Latin1.Text(entry.Key)));
        Assert.Equal(new List<object?> { page, new ParsedName("XYZ"), 0L, 100L, null }, destinations[0].Value);
        Assert.Equal(new List<object?> { page, new ParsedName("XYZ"), 10L, 90.5, null }, destinations[1].Value);
    }

    [Fact]
    public void RefusesANullDestinationName()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());

        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => document.AddNamedDestination(null!, new PdfReference(1), 0, 0));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void WritesTheInformationDictionary()
    {
        PdfFileReader reader = Write(document =>
        {
            document.Info.Title = "Report";
            document.Info.CreationDate = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.FromHours(4));
            document.EndPage(document.BeginPage(10, 10));
        });

        Dictionary<string, object?> info = reader.Dictionary(reader.Trailer["Info"]);
        Assert.Equal("Report", Latin1.Text((byte[])info["Title"]!));
        Assert.Equal("D:20260926100000+04'00'", Latin1.Text((byte[])info["CreationDate"]!));
    }

    [Fact]
    public void ReusesOneGraphicsStatePerDistinctOpacity()
    {
        PdfReference half = default;
        PdfReference mixed = default;

        PdfFileReader reader = Write(document =>
        {
            half = document.GetOpacityState(0.5);
            Assert.Equal(half, document.GetOpacityState(0.5, 0.5));
            Assert.Equal(half, document.GetOpacityState(0.500004));
            Assert.NotEqual(half, document.GetOpacityState(0.50001));
            mixed = document.GetOpacityState(0.5, 0.25);
            Assert.NotEqual(half, mixed);
            Assert.NotEqual(mixed, document.GetOpacityState(0.25, 0.5));
            Assert.Equal(mixed, document.GetOpacityState(0.5, 0.25));
            document.EndPage(document.BeginPage(10, 10));
        });

        Dictionary<string, object?> state = reader.Dictionary(new ParsedReference(mixed.ObjectNumber, 0));
        Assert.Equal(new ParsedName("ExtGState"), state["Type"]);
        Assert.Equal(0.5, state["ca"]);
        Assert.Equal(0.25, state["CA"]);
    }

    [Theory]
    [InlineData(0.0, 1.0, null)]
    [InlineData(-0.001, 1.0, "fillAlpha")]
    [InlineData(1.001, 1.0, "fillAlpha")]
    [InlineData(double.NaN, 1.0, "fillAlpha")]
    [InlineData(1.0, -0.001, "strokeAlpha")]
    [InlineData(0.0, 1.001, "strokeAlpha")]
    public void AcceptsOpacitiesFromZeroToOne(double fill, double stroke, string? rejected)
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());

        if (rejected == null)
        {
            Assert.Equal(1, document.GetOpacityState(fill, stroke).ObjectNumber);
        }
        else
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => document.GetOpacityState(fill, stroke));
            Assert.Equal(rejected, exception.ParamName);
            Assert.StartsWith("Opacity runs from 0 to 1.", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefusesToEndAPageTwiceOrOneFromElsewhere()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        using PdfDocumentWriter other = new PdfDocumentWriter(new MemoryStream());
        PdfPage page = document.BeginPage(10, 10);
        PdfPage foreign = other.BeginPage(10, 10);
        document.EndPage(page);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.EndPage(page));
        Assert.Throws<InvalidOperationException>(() => document.EndPage(foreign));
        Assert.Throws<ArgumentNullException>(() => document.EndPage(null!));

        Assert.Equal("The page is not open in this document.", exception.Message);
    }

    [Fact]
    public void RefusesToEndAPageWithUnbalancedContent()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        PdfPage page = document.BeginPage(10, 10);
        page.Content.SaveState();

        Assert.Throws<InvalidOperationException>(() => document.EndPage(page));

        page.Content.RestoreState();
        document.EndPage(page);
    }

    [Fact]
    public void RefusesToFinishWithPagesStillOpen()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        document.BeginPage(10, 10);
        document.BeginPage(10, 10);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => document.Finish());

        Assert.Equal("2 page(s) were begun but never ended.", exception.Message);
    }

    [Fact]
    public void RefusesEverythingOnceFinished()
    {
        using PdfDocumentWriter document = new PdfDocumentWriter(new MemoryStream());
        PdfPage page = document.BeginPage(10, 10);
        document.EndPage(page);
        document.Finish();

        static string Refusal(Action action) => Assert.Throws<InvalidOperationException>(action).Message;

        Assert.Equal("The document has been finished.", Refusal(() => document.Finish()));
        Assert.Equal("The document has been finished.", Refusal(() => document.BeginPage(10, 10)));
        Assert.Equal("The document has been finished.", Refusal(() => document.EndPage(page)));
        Assert.Equal("The document has been finished.", Refusal(() => document.AddNamedDestination("x", page.Reference, 0, 0)));
        Assert.Equal("The document has been finished.", Refusal(() => document.GetOpacityState(1)));
    }

    [Theory]
    [InlineData(EncryptionLevel.AesWith256Bits, false, 8L)]
    [InlineData(EncryptionLevel.AesWith128Bits, false, null)]
    [InlineData(EncryptionLevel.AesWith256Bits, true, 3L)]
    public void Aes256IsDeclaredAsAdobesExtensionLevel8UnlessTheCatalogDeclaresItsOwn(EncryptionLevel level, bool declared, long? expected)
    {
        using MemoryStream output = new MemoryStream();
        // Outside an object stream the catalog is read as it is: only strings and streams are encrypted.
        PdfWriterOptions options = new PdfWriterOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Table,
            Encryption = Rustaveli.Pdf.Security.PdfEncryption.Create(new Protection { Encryption = level }),
        };

        using (PdfDocumentWriter document = new PdfDocumentWriter(output, options))
        {
            if (declared)
            {
                document.Catalog[new PdfName("Extensions")] = new PdfDictionary
                {
                    [new PdfName("ADBE")] = new PdfDictionary { [new PdfName("BaseVersion")] = new PdfName("1.7"), [new PdfName("ExtensionLevel")] = 3 },
                };
            }

            document.EndPage(document.BeginPage(10, 10));
            document.Finish();
        }

        Dictionary<string, object?> catalog = new PdfFileReader(output.ToArray()).Catalog();

        if (expected is null)
        {
            Assert.False(catalog.ContainsKey("Extensions"));
            return;
        }

        Dictionary<string, object?> adobe = (Dictionary<string, object?>)((Dictionary<string, object?>)catalog["Extensions"]!)["ADBE"]!;
        Assert.Equal(new ParsedName("1.7"), adobe["BaseVersion"]);
        Assert.Equal(expected, adobe["ExtensionLevel"]);
    }

    [Theory]
    [InlineData(EncryptionLevel.AesWith256Bits)]
    [InlineData(EncryptionLevel.AesWith128Bits)]
    [InlineData(EncryptionLevel.Rc4With128Bits)]
    public void TheCatalogOfAnEncryptedFileIsWrittenOutsideTheObjectStreams(EncryptionLevel level)
    {
        // Readers look up the catalog while reading the trailer, before decryption is set up — PdfPig among them — so
        // a catalog inside an encrypted object stream leaves them a file they cannot read.
        using MemoryStream output = new MemoryStream();
        PdfWriterOptions options = new PdfWriterOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
            Encryption = Rustaveli.Pdf.Security.PdfEncryption.Create(new Protection { Encryption = level }),
        };

        using (PdfDocumentWriter document = new PdfDocumentWriter(output, options))
        {
            document.EndPage(document.BeginPage(10, 10));
            document.Finish();
        }

        PdfFileReader reader = new PdfFileReader(output.ToArray());
        ParsedReference root = (ParsedReference)reader.Trailer["Root"]!;

        Assert.True(reader.HasCrossReferenceStream);
        Assert.Equal(1, reader.Entries[root.ObjectNumber].Type);
        Assert.Contains(reader.Entries.Values, entry => entry.Type == 2);
    }

    [Fact]
    public void RefusesToFinishWithoutPages()
    {
        using MemoryStream output = new MemoryStream();
        using PdfDocumentWriter document = new PdfDocumentWriter(output);
        document.Info.Title = "Empty";

        Assert.Throws<InvalidOperationException>(() => document.Finish());
        Assert.DoesNotContain("Catalog", Latin1.Text(output.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public void ReleasesOpenPagesWithoutFinishing()
    {
        using MemoryStream output = new MemoryStream();
        PdfDocumentWriter document = new PdfDocumentWriter(output);
        document.BeginPage(10, 10).Content.Rectangle(0, 0, 1, 1);

        document.Dispose();

        Assert.DoesNotContain("%%EOF", Latin1.Text(output.ToArray()), StringComparison.Ordinal);
    }
}
