using System.Globalization;
using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Saving a file put together from others, built by hand to say what generated files never do: pages without
/// resources or content, entries of the wrong kind, links and fields that lead nowhere once pages are left out.
/// </summary>
public class FileAssemblyTests
{
    private static readonly PdfName AcroForm = new PdfName("AcroForm");
    private static readonly PdfName Fields = new PdfName("Fields");
    private static readonly PdfName DR = new PdfName("DR");
    private static readonly PdfName T = new PdfName("T");
    private static readonly PdfName Dest = new PdfName("Dest");

    /// <summary>The file <paramref name="pdf"/> holds, opened once its cross-reference section is written.</summary>
    private static PdfFile Opened(HandmadePdf pdf, string trailer = "/Root 1 0 R")
    {
        pdf.Section(trailer);
        return PdfFile.Open(pdf.ToArray());
    }

    private static PdfSource Saved(PdfFile file) => PdfSource.Open(file.ToArray());

    /// <summary>A file of one page whose catalog adds <paramref name="catalog"/>.</summary>
    private static HandmadePdf Catalogued(string catalog, string page = "") =>
        new HandmadePdf()
            .Object(1, $"<</Type/Catalog/Pages 2 0 R{catalog}>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, $"<</Type/Page/Parent 2 0 R{page}>>");

    /// <summary>A page to lay on others, 200 points square unless <paramref name="page"/> says, drawing one line.</summary>
    private static PdfFile Stamp(string page = "/MediaBox[0 0 200 200]") =>
        Opened(HandmadePdf.OnePage(page + "/Resources<</ProcSet[/PDF]>>/Contents 4 0 R").Stream(4, "<<>>", "0 0 m 9 9 l S"));

    /// <summary>The content streams of a page of <paramref name="source"/>, each decoded.</summary>
    private static List<string> Contents(PdfSource source, int page)
    {
        PdfValue contents = source.Pages[page].Dictionary[PdfNames.Contents];
        IEnumerable<PdfValue> streams = source.Resolve(contents) is { Kind: PdfValueKind.Array } array ? array.AsArray().Cast<PdfValue>() : [contents];
        return streams.Select(stream => Encoding.Latin1.GetString(source.Decode(source.Stream(stream)!))).ToList();
    }

    private static PdfDictionary Dictionary(PdfSource source, PdfValue value) => source.Resolve(value).AsDictionary();

    [Theory]
    [InlineData("", false)]
    [InlineData("/Annots 7", true)]
    public void APageKeptTwiceWithNoAnnotationsToCopyKeepsWhatItHad(string page, bool listed)
    {
        PdfSource source = Saved(Opened(HandmadePdf.OnePage(page)).KeepPages("1, 1"));

        Assert.Equal(2, source.Pages.Count);
        Assert.All(source.Pages, kept => Assert.Equal(listed, kept.Dictionary.ContainsKey(PdfNames.Annots)));

        if (listed)
            Assert.All(source.Pages, kept => Assert.Equal(7L, kept.Dictionary[PdfNames.Annots].AsInteger()));
    }

    [Fact]
    public void APageKeptTwiceHasEachOfItsAnnotationsWrittenAnewForIt()
    {
        HandmadePdf pdf = HandmadePdf.OnePage("/Annots[5 0 R]").Object(5, "<</Type/Annot/Subtype/Square/Rect[0 0 9 9]/P 3 0 R>>");

        PdfSource source = Saved(Opened(pdf).KeepPages("1, 1"));

        List<PdfReference> annotations = source.Pages.Select(page => Assert.Single(source.Resolve(page.Dictionary[PdfNames.Annots]).AsArray()).AsReference()).ToList();

        Assert.NotEqual(annotations[0].ObjectNumber, annotations[1].ObjectNumber);
        Assert.Equal(source.Pages[1].ObjectNumber, Dictionary(source, annotations[1])[new PdfName("P")].AsReference().ObjectNumber);
    }

    [Theory]
    [InlineData("", "q\n|\nQ\nq\n/Layer0 Do\nQ\n")]
    [InlineData("/Contents 4 0 R", "q\n|0 0 m|\nQ\nq\n/Layer0 Do\nQ\n")]
    [InlineData("/Contents[4 0 R 5 0 R]", "q\n|0 0 m|9 9 l S|\nQ\nq\n/Layer0 Do\nQ\n")]
    public void APageLaidOnKeepsEachOfItsContentStreamsBetweenWhatIsLaid(string contents, string expected)
    {
        HandmadePdf page = HandmadePdf.OnePage("/MediaBox[0 0 200 200]" + contents).Stream(4, "<<>>", "0 0 m").Stream(5, "<<>>", "9 9 l S");

        PdfSource source = Saved(Opened(page).Overlay(Stamp()));

        Assert.Equal(expected.Split('|'), Contents(source, 0));
    }

    [Theory]
    [InlineData("", "XObject")]
    [InlineData("/Resources 7", "XObject")]
    [InlineData("/Resources<</ProcSet[/PDF]>>", "ProcSet XObject")]
    [InlineData("/Resources<</XObject 7/ProcSet[/PDF]>>", "ProcSet XObject")]
    [InlineData("/Resources<</XObject<</Own 4 0 R>>/ProcSet[/PDF]>>", "ProcSet XObject")]
    public void APageLaidOnKeepsTheResourcesItCanReadBesideWhatIsLaid(string resources, string kept)
    {
        HandmadePdf page = HandmadePdf.OnePage("/MediaBox[0 0 200 200]" + resources).Stream(4, "<</Type/XObject/Subtype/Form/BBox[0 0 1 1]>>", "");

        PdfSource source = Saved(Opened(page).Overlay(Stamp()));
        PdfDictionary written = Dictionary(source, source.Pages[0].Dictionary[PdfNames.Resources]);
        PdfDictionary forms = Dictionary(source, written[PdfNames.XObject]);

        Assert.Equal(kept, string.Join(" ", written.Select(entry => entry.Key.Value).OrderBy(key => key, StringComparer.Ordinal)));
        Assert.Equal(resources.Contains("/Own") ? "Layer0 Own" : "Layer0", string.Join(" ", forms.Select(entry => entry.Key.Value).OrderBy(key => key, StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("/Resources<</ProcSet[/PDF]>>", "ProcSet")]
    public void APageLaidAsAFormBringsAllItsContentAndTheResourcesItHas(string resources, string kept)
    {
        PdfFile stamp = Opened(HandmadePdf.OnePage("/MediaBox[0 0 200 200]/Contents[4 0 R 5 0 R]" + resources).Stream(4, "<<>>", "0 0 m").Stream(5, "<<>>", "9 9 l S"));

        PdfSource source = Saved(Opened(HandmadePdf.OnePage("/MediaBox[0 0 200 200]")).Overlay(stamp));
        PdfValue form = Dictionary(source, Dictionary(source, source.Pages[0].Dictionary[PdfNames.Resources])[PdfNames.XObject])[new PdfName("Layer0")];

        Assert.Equal("0 0 m\n9 9 l S\n", Encoding.Latin1.GetString(source.Decode(source.Stream(form)!)));
        Assert.Equal(kept, string.Join(" ", Dictionary(source, source.Stream(form)!.Dictionary[PdfNames.Resources]).Select(entry => entry.Key.Value)));
    }

    [Theory]
    [InlineData("", "1 0 0 1 0 50 cm\n")]
    [InlineData("/Rotate 90", "0 -1 1 0 0 250 cm\n")]
    [InlineData("/Rotate 90.0", "1 0 0 1 0 50 cm\n")]
    [InlineData("/Rotate/East", "1 0 0 1 0 50 cm\n")]
    [InlineData("/Rotate 45", "1 0 0 1 0 50 cm\n")]
    [InlineData("/CropBox[0.0 0 200 200.0]", "1 0 0 1 0 50 cm\n")]
    [InlineData("/CropBox[0 0 200 0]", "")]
    [InlineData("/CropBox[0 0 0 200]", "")]
    [InlineData("/CropBox[0 0 /Wide 200]", "")]
    public void APageIsTurnedOnlyByARotationOfWholeDegreesAndPlacedOnlyByABoxEnclosingSomething(string stamp, string placement)
    {
        PdfSource source = Saved(Opened(HandmadePdf.OnePage("/MediaBox[0 0 200 300]")).Overlay(Stamp("/MediaBox[0 0 200 200]" + stamp)));

        Assert.Equal($"\nQ\nq\n{placement}/Layer0 Do\nQ\n", Contents(source, 0).Last());
    }

    [Theory]
    [InlineData("/OCProperties<</OCGs[4 0 R]>>", 1)]
    [InlineData("/OCProperties<</OCGs[4 0 R]/D 7>>", 1)]
    [InlineData("/OCProperties<</OCGs[4 0 R]/D<<>>>>", 1)]
    [InlineData("/OCProperties<</OCGs 7>>", 0)]
    public void OptionalContentWhoseConfigurationHidesNothingIsSavedHidingNothing(string catalog, int groups)
    {
        PdfSource source = Saved(Opened(Catalogued(catalog).Object(4, "<</Type/OCG/Name(a)>>")));
        PdfDictionary properties = Dictionary(source, source.Catalog[new PdfName("OCProperties")]);
        PdfDictionary configuration = Dictionary(source, properties[PdfNames.D]);

        Assert.Equal(groups, source.Resolve(properties[new PdfName("OCGs")]).AsArray().Count);
        Assert.Empty(source.Resolve(configuration[new PdfName("OFF")]).AsArray());
    }

    /// <summary>
    /// Two pages with a form: one text field, named by the hex string <paramref name="name"/>, whose widget is on the
    /// first page; the form's fields listed as <paramref name="fields"/>, with <paramref name="form"/> added to it.
    /// </summary>
    private static HandmadePdf Form(string name = "<6E616D65>", string fields = "[5 0 R]", string form = "") =>
        new HandmadePdf()
            .Object(1, $"<</Type/Catalog/Pages 2 0 R/AcroForm<</Fields{fields}{form}>>>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Annots[5 0 R]>>")
            .Object(4, "<</Type/Page/Parent 2 0 R>>")
            .Object(5, $"<</Type/Annot/Subtype/Widget/FT/Tx/T{name}/Rect[0 0 9 9]/P 3 0 R>>");

    /// <summary>The names of the fields of the form <paramref name="source"/> holds, each as hex.</summary>
    private static List<string> FieldNames(PdfSource source) =>
        source.Resolve(Dictionary(source, source.Catalog[AcroForm])[Fields]).AsArray().Cast<PdfValue>()
            .Select(field => string.Concat(Dictionary(source, field)[T].AsString().Bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture))))
            .ToList();

    [Theory]
    [InlineData("FEFF0061", "FEFF0061002B0031")]
    [InlineData("61", "612B31")]
    [InlineData("FE61", "FE612B31")]
    public void AFieldNamedAsOneAlreadyListedIsRenamedInTheEncodingOfItsName(string name, string renamed)
    {
        HandmadePdf pdf = Form($"<{name}>");
        pdf.Section("/Root 1 0 R");

        PdfSource source = Saved(PdfFile.Open(pdf.ToArray()).Append(PdfFile.Open(pdf.ToArray())));

        Assert.Equal([name, renamed], FieldNames(source));
    }

    [Fact]
    public void ResourcesOfTheFormAreJoinedAndWhatIsNotADictionaryKeepsWhatTheFirstFileGave()
    {
        string font = "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>";
        PdfSource source = Saved(Opened(Form(form: $"/DR<</ProcSet[/PDF]/Font<</Helv {font}>>>>"))
            .Append(Opened(Form(form: $"/DR<</ProcSet[/Text]/Font<</Helv {font}/ZaDb {font}>>>>"))));
        PdfDictionary resources = Dictionary(source, Dictionary(source, source.Catalog[AcroForm])[DR]);

        Assert.Equal(["PDF"], source.Resolve(resources[new PdfName("ProcSet")]).AsArray().Cast<PdfValue>().Select(name => name.AsName().Value));
        Assert.Equal(["Helv", "ZaDb"], Dictionary(source, resources[PdfNames.Font]).Select(entry => entry.Key.Value));
    }

    [Fact]
    public void TheFirstFilesFormIsKeptWithNoFieldsWhileItsPagesAllAre()
    {
        PdfSource source = Saved(Opened(Form(fields: "[]", form: "/DA(/Helv 0 Tf 0 g)")));
        PdfDictionary form = Dictionary(source, source.Catalog[AcroForm]);

        Assert.Empty(source.Resolve(form[Fields]).AsArray());
        Assert.False(form.ContainsKey(DR));
        Assert.True(form.ContainsKey(new PdfName("DA")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFormNoneOfWhoseFieldsAreKeptIsLeftOut(bool appended)
    {
        PdfFile file = appended
            ? Opened(HandmadePdf.OnePage()).Append(Opened(Form()), "2")
            : Opened(Form()).KeepPages("2");

        PdfSource source = Saved(file);

        Assert.Equal(appended ? 2 : 1, source.Pages.Count);
        Assert.False(source.Catalog.ContainsKey(AcroForm));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheRestrictionsASignaturePlacesAreKeptUnlessLifted(bool lifted)
    {
        PdfFile file = Opened(Catalogued("/Perms<</DocMDP 4 0 R>>").Object(4, "<</Type/Sig/Filter/Adobe.PPKLite>>"));

        if (lifted)
            file.LiftRestrictions();

        Assert.Equal(!lifted, Saved(file).Catalog.ContainsKey(new PdfName("Perms")));
    }

    [Theory]
    [InlineData("/Names 7", "", "Names")]
    [InlineData("/AF 7", "", "AF")]
    [InlineData("", "/Info 7", "Info")]
    public void WhatTheFirstFileSaysOfItselfAsSomethingItCannotBeIsLeftOut(string catalog, string trailer, string key)
    {
        PdfSource source = Saved(Opened(Catalogued(catalog), "/Root 1 0 R" + trailer));

        Assert.False(source.Catalog.ContainsKey(new PdfName(key)));
        Assert.False(source.Trailer.ContainsKey(new PdfName(key)));
    }

    [Fact]
    public void TheFilesAssociatedWithTheDocumentAndItsInformationAreKept()
    {
        PdfSource source = Saved(Opened(Catalogued("/AF[4 0 R]").Object(4, "<</Type/Filespec/F(a.txt)>>").Object(5, "<</Title(Kept)>>"), "/Root 1 0 R/Info 5 0 R"));

        PdfDictionary specification = Dictionary(source, Assert.Single(source.Resolve(source.Catalog[new PdfName("AF")]).AsArray()));
        PdfDictionary information = Dictionary(source, source.Trailer[new PdfName("Info")]);

        Assert.Equal("a.txt", Encoding.ASCII.GetString(specification[new PdfName("F")].AsString().Bytes.ToArray()));
        Assert.Equal("Kept", Encoding.ASCII.GetString(information[new PdfName("Title")].AsString().Bytes.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataIsAddedToThePacketThereOrToANewOneWhenThereIsNone(bool packet)
    {
        string existing = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:o=\"urn:old\"><o:Old>there</o:Old></rdf:Description></rdf:RDF></x:xmpmeta>";
        HandmadePdf pdf = Catalogued("/Metadata 4 0 R");
        pdf = packet ? pdf.Stream(4, "<</Type/Metadata/Subtype/XML>>", existing) : pdf.Object(4, "<</Type/Metadata/Subtype/XML>>");

        PdfSource source = Saved(Opened(pdf).AddMetadata("<rdf:Description rdf:about=\"\" xmlns:k=\"urn:kind\"><k:Kind>added</k:Kind></rdf:Description>"));
        string metadata = Encoding.UTF8.GetString(source.Decode(source.Stream(source.Catalog[new PdfName("Metadata")])!));

        Assert.Contains("adobe:ns:meta/", metadata, StringComparison.Ordinal);
        Assert.Contains("<k:Kind>added</k:Kind>", metadata, StringComparison.Ordinal);
        Assert.Equal(packet, metadata.Contains("<o:Old>there</o:Old>"));
    }

    [Fact]
    public void ALinkToAPageLeftOutLeadsNowhere()
    {
        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Annots[<</Type/Annot/Subtype/Link/Rect[0 0 9 9]/Dest[4 0 R/Fit]>>]>>")
            .Object(4, "<</Type/Page/Parent 2 0 R>>");

        PdfSource source = Saved(Opened(pdf).KeepPages("1"));
        PdfArray destination = Dictionary(source, source.Resolve(source.Pages[0].Dictionary[PdfNames.Annots]).AsArray()[0])[Dest].AsArray();

        Assert.Equal(PdfValueKind.Null, destination[0].Kind);
        Assert.Equal("Fit", destination[1].AsName().Value);
    }

    [Fact]
    public void OnlyAGoToActionNamingADestinationTheFileHasIsTakenToThePlaceItNames()
    {
        string link = "<</Type/Annot/Subtype/Link/Rect[0 0 9 9]/A";
        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R/Dests<</there[4 0 R/Fit]>>>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>")
            .Object(3, $"<</Type/Page/Parent 2 0 R/Annots[{link}<</S 7/D/there>>>>{link}<</S/GoTo>>>>{link}<</S/GoToR/F(other.pdf)/D/there>>>>"
                + $"{link}<</S/GoTo/D/elsewhere>>>>{link}<</S/GoTo/D/there>>>>]>>")
            .Object(4, "<</Type/Page/Parent 2 0 R>>");

        PdfSource source = Saved(Opened(HandmadePdf.OnePage()).Append(Opened(pdf)));
        List<PdfDictionary> actions = source.Resolve(source.Pages[1].Dictionary[PdfNames.Annots]).AsArray().Cast<PdfValue>()
            .Select(annotation => Dictionary(source, Dictionary(source, annotation)[PdfNames.A]))
            .ToList();

        Assert.Equal("there", actions[0][PdfNames.D].AsName().Value);
        Assert.False(actions[1].ContainsKey(PdfNames.D));
        Assert.Equal("there", actions[2][PdfNames.D].AsName().Value);
        Assert.Equal("elsewhere", actions[3][PdfNames.D].AsName().Value);
        Assert.Equal(source.Pages[2].ObjectNumber, source.Resolve(actions[4][PdfNames.D]).AsArray()[0].AsReference().ObjectNumber);
    }

    [Fact]
    public void AnObjectThatOnlyRefersToAnotherIsWrittenAsWhatItRefersTo()
    {
        HandmadePdf pdf = HandmadePdf.OnePage("/MediaBox 6 0 R/Resources<</Font<</F1 5 0 R>>>>")
            .Object(5, "7 0 R")
            .Object(6, "[0 0 200 200]")
            .Object(7, "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        PdfSource source = Saved(Opened(pdf));
        PdfDictionary page = source.Pages[0].Dictionary;
        PdfValue font = Dictionary(source, Dictionary(source, page[PdfNames.Resources])[PdfNames.Font])[new PdfName("F1")];
        PdfValue written = (PdfValue)source.GetObject(font.AsReference().ObjectNumber);
        PdfValue box = (PdfValue)source.GetObject(page[PdfNames.MediaBox].AsReference().ObjectNumber);

        Assert.Equal("Helvetica", written.AsDictionary()[new PdfName("BaseFont")].AsName().Value);
        Assert.Equal([0L, 0L, 200L, 200L], box.AsArray().Cast<PdfValue>().Select(corner => corner.AsInteger()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/plain")]
    public void AnAttachmentIsGivenASubtypeOnlyByAMediaTypeThatSaysSomething(string? mediaType)
    {
        PdfFile file = Opened(HandmadePdf.OnePage()).Attach(new FileAttachment("a.txt", "a"u8.ToArray()) { MediaType = mediaType });

        PdfSource source = Saved(file);
        SourceStream attached = source.ObjectNumbers.Select(source.GetObject).OfType<SourceStream>()
            .Single(stream => stream.Dictionary.TryGetValue(PdfNames.Type, out PdfValue type) && type.AsName().Value == "EmbeddedFile");

        Assert.Equal(string.IsNullOrEmpty(mediaType) ? null : mediaType, attached.Dictionary.TryGetValue(PdfNames.Subtype, out PdfValue subtype) ? subtype.AsName().Value : null);
    }

    [Fact]
    public void SavingToAPathWritesTheFileThereWhetherOrNotOneWasThere()
    {
        string directory = Path.Combine(Path.GetTempPath(), "assembly-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "new.pdf");

            Opened(HandmadePdf.OnePage()).KeepPages("1, 1").Save(path);
            int first = PdfFile.Open(path).PageCount;

            // And over that file, once it is there.
            PdfFile.Open(path).KeepPages("1, 1, 2").Save(path);

            Assert.Equal(2, first);
            Assert.Equal(3, PdfFile.Open(path).PageCount);
            Assert.Equal([path], Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ASaveThatFailsLeavesWhatWasThereAndNothingBeside()
    {
        string directory = Path.Combine(Path.GetTempPath(), "assembly-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "kept.pdf");
            byte[] original = Encoding.ASCII.GetBytes("what was there");
            File.WriteAllBytes(path, original);

            // Metadata that is not XML is refused only as the file is written, once the partial file is begun.
            PdfFile file = Opened(HandmadePdf.OnePage()).AddMetadata("<unclosed>");

            Assert.Throws<ArgumentException>(() => file.Save(path));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal([path], Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
