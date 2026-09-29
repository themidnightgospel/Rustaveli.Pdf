using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace Rustaveli.Pdf.IntegrationTests.Operations;

/// <summary>
/// Putting files together: pages kept and reordered, files appended, pages laid over and beneath, and what the first
/// file says of the document carried across — read back by an independent reader.
/// </summary>
public class PdfFileTests
{
    /// <summary>A document of pages, each showing its label at the top, each bookmarked.</summary>
    private static byte[] Pages(params string[] labels) => Pages(0, labels);

    /// <summary>Pages to lay over or beneath others, their labels lower down so the words stay apart.</summary>
    private static byte[] Layer(params string[] labels) => Pages(100, labels);

    private static byte[] Pages(float top, string[] labels)
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Stack(stack =>
            {
                for (int index = 0; index < labels.Length; index++)
                {
                    if (index > 0)
                        stack.Add().NewPage();

                    stack.Add().InsetTop(top).Bookmark(labels[index]).Anchor(labels[index]).Text(labels[index]);
                }
            });
        }));

        document.Info.Title = "Pages " + string.Join(" ", labels);
        return document.ExportPdf();
    }

    /// <summary>The words of each page, as the reader finds them.</summary>
    private static List<string> Read(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => string.Join(" ", page.GetWords().Select(word => word.Text))).ToList();
    }

    /// <summary>The letters of each page in the order they are drawn, beneath first.</summary>
    private static List<string> Drawn(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => string.Concat(page.Letters.Select(letter => letter.Value))).ToList();
    }

    [Fact]
    public void AFileSavedUnchangedKeepsEveryPageAndWhatItSaysOfItself()
    {
        byte[] saved = PdfFile.Open(Pages("One", "Two", "Three")).ToArray();

        Assert.Equal(["One", "Two", "Three"], Read(saved));

        using PdfDocument document = PdfDocument.Open(saved);
        Assert.Equal("Pages One Two Three", document.Information.Title);
        Assert.True(document.TryGetBookmarks(out Bookmarks? bookmarks));
        Assert.Equal(["One", "Two", "Three"], bookmarks!.Roots.Select(node => node.Title));
    }

    [Fact]
    public void PagesAreKeptInTheOrderNamed()
    {
        PdfFile file = PdfFile.Open(Pages("One", "Two", "Three", "Four")).KeepPages("3, 1-2, last");

        Assert.Equal(4, file.PageCount);
        Assert.Equal(["Three", "One", "Two", "Four"], Read(file.ToArray()));
    }

    [Theory]
    [InlineData("2-", "Two Three")]
    [InlineData("-2", "One Two")]
    [InlineData("3-1", "Three Two One")]
    [InlineData(" 1 , 1 ", "One One")]
    [InlineData("LAST-last", "Three")]
    public void PageListsReadAsPrintDialogsReadThem(string pages, string expected) =>
        Assert.Equal(expected, string.Join(" ", Read(PdfFile.Open(Pages("One", "Two", "Three")).KeepPages(pages).ToArray())));

    [Theory]
    [InlineData("")]
    [InlineData("1,,2")]
    [InlineData("one")]
    [InlineData("1-2-3")]
    [InlineData("+1")]
    public void APageListThatIsNotOneIsRefused(string pages)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => PdfFile.Open(Pages("One", "Two")).KeepPages(pages));

        Assert.Equal("pages", exception.ParamName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("1-9")]
    public void APageNotInTheFileIsRefused(string pages)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => PdfFile.Open(Pages("One", "Two")).KeepPages(pages));

        Assert.Contains("not among the 2 pages", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilesAreAppendedWholeOrInPart()
    {
        PdfFile file = PdfFile.Open(Pages("One", "Two"))
            .Append(PdfFile.Open(Pages("Three", "Four", "Five")), "3, 1")
            .Append(PdfFile.Open(Pages("Six")));

        Assert.Equal(["One", "Two", "Five", "Three", "Six"], Read(file.ToArray()));
    }

    [Fact]
    public void AppendingKeepsTheFirstFilesOutlineWhileItsPagesLeadInOrder()
    {
        byte[] appended = PdfFile.Open(Pages("One", "Two")).Append(PdfFile.Open(Pages("Three"))).ToArray();
        byte[] reordered = PdfFile.Open(Pages("One", "Two")).KeepPages("2, 1").ToArray();

        using PdfDocument whole = PdfDocument.Open(appended);
        using PdfDocument cut = PdfDocument.Open(reordered);

        Assert.True(whole.TryGetBookmarks(out Bookmarks? kept));
        Assert.Equal(["One", "Two"], kept!.Roots.Select(node => node.Title));
        Assert.False(cut.TryGetBookmarks(out _));
        Assert.Equal("Pages One Two", cut.Information.Title);
    }

    [Fact]
    public void AStampGoesOverEveryPageInTurn()
    {
        byte[] stamped = PdfFile.Open(Pages("One", "Two", "Three"))
            .Overlay(PdfFile.Open(Layer("Copy", "Draft")))
            .ToArray();

        Assert.Equal(["One Copy", "Two Draft", "Three Copy"], Read(stamped));
        Assert.Equal(["OneCopy", "TwoDraft", "ThreeCopy"], Drawn(stamped));
    }

    [Fact]
    public void ALetterheadGoesBeneathTheNamedPages()
    {
        byte[] headed = PdfFile.Open(Pages("One", "Two", "Three"))
            .Underlay(PdfFile.Open(Layer("Letterhead", "Other")), onto: "1, 3", from: "1")
            .ToArray();

        Assert.Equal(["LetterheadOne", "Two", "LetterheadThree"], Drawn(headed));
    }

    [Fact]
    public void LayersFollowTheirPagesWhenPagesMove()
    {
        byte[] moved = PdfFile.Open(Pages("One", "Two"))
            .Overlay(PdfFile.Open(Layer("Stamp")), onto: "2")
            .KeepPages("2, 1")
            .ToArray();

        Assert.Equal(["Two Stamp", "One"], Read(moved));
    }

    [Fact]
    public void OneStampLaidOnFilesSavedAtOnceGoesOnEachAsOnOne()
    {
        byte[] page = Pages("One");
        byte[] stamp = Layer("Stamp");
        byte[] expected = PdfFile.Open(page).Overlay(PdfFile.Open(stamp)).ToArray();

        for (int round = 0; round < 10; round++)
        {
            // The files share the stamp's pages, and with them the one reading of its file.
            PdfFile shared = PdfFile.Open(stamp);
            PdfFile[] files = Enumerable.Range(0, 8).Select(_ => PdfFile.Open(page).Overlay(shared)).ToArray();
            byte[][] saved = new byte[files.Length][];

            Parallel.For(0, files.Length, index => saved[index] = files[index].ToArray());

            Assert.All(saved, file => Assert.Equal(expected, file));
        }
    }

    /// <summary>How many form XObjects the file holds.</summary>
    private static int Forms(byte[] pdf)
    {
        PdfSource source = PdfSource.Open(pdf);
        return source.ObjectNumbers.Count(number => source.GetObject(number) is SourceStream stream
            && stream.Dictionary.TryGetValue(PdfNames.Subtype, out PdfValue subtype) && subtype.Kind == PdfValueKind.Name && subtype.AsName().Value == "Form");
    }

    [Fact]
    public void APageLaidOnManyIsWrittenOnce()
    {
        byte[] pages = Pages("One", "Two", "Three");
        byte[] laid = PdfFile.Open(pages)
            .Overlay(PdfFile.Open(Layer("Stamp")))
            .Underlay(PdfFile.Open(Layer("Head")), onto: "1-2")
            .ToArray();

        Assert.Equal(["HeadOneStamp", "HeadTwoStamp", "ThreeStamp"], Drawn(laid));
        Assert.Equal(Forms(pages) + 2, Forms(laid));
    }

    /// <summary>
    /// A page of <paramref name="width"/> by <paramref name="height"/>, turned by <paramref name="rotate"/>, showing
    /// <paramref name="text"/> near its lower left corner.
    /// </summary>
    private static byte[] Turned(int width, int height, int rotate, string text, string? box = null, string extra = "", string catalog = "")
    {
        string content = $"BT /F1 12 Tf 20 20 Td ({text}) Tj ET";
        string pdf = $"%PDF-1.7\n1 0 obj<</Type/Catalog/Pages 2 0 R{catalog}>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n"
            + $"3 0 obj<</Type/Page/Parent 2 0 R/MediaBox {box ?? $"[0 0 {width} {height}]"}/Rotate {rotate}{extra}/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>endobj\n"
            + "4 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n"
            + $"5 0 obj<</Length {content.Length}>>stream\n{content}\nendstream\nendobj\ntrailer<</Root 1 0 R>>\n%%EOF";

        // Saved once, so that it has the cross-reference section the independent reader needs.
        return PdfFile.Open(System.Text.Encoding.ASCII.GetBytes(pdf)).ToArray();
    }

    /// <summary>
    /// Which way the word <paramref name="word"/> runs on the first page as it is seen, the page turned as it says:
    /// across and up, each -1, 0 or 1.
    /// </summary>
    private static (int Across, int Up) Direction(byte[] pdf, string word)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        List<UglyToad.PdfPig.Content.Letter> letters = document.GetPage(1).Letters.Where(letter => word.Contains(letter.Value)).ToList();
        Assert.Equal(word, string.Concat(letters.Select(letter => letter.Value)));

        double across = letters[letters.Count - 1].EndBaseLine.X - letters[0].StartBaseLine.X;
        double up = letters[letters.Count - 1].EndBaseLine.Y - letters[0].StartBaseLine.Y;
        return (Math.Sign(Math.Round(across)), Math.Sign(Math.Round(up)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 90)]
    [InlineData(90, 0)]
    [InlineData(90, 90)]
    [InlineData(0, 180)]
    [InlineData(270, 0)]
    [InlineData(90, 270)]
    public void AStampIsTurnedToBeSeenAsOnItsOwnPage(int stampRotate, int pageRotate)
    {
        byte[] stamp = Turned(200, 200, stampRotate, "Stamp");
        byte[] stamped = PdfFile.Open(Turned(200, 300, pageRotate, "XYZ")).Overlay(PdfFile.Open(stamp)).ToArray();

        Assert.Equal(Direction(stamp, "Stamp"), Direction(stamped, "Stamp"));

        // And it is on the page, not turned off it.
        using PdfDocument document = PdfDocument.Open(stamped);
        UglyToad.PdfPig.Content.Page page = document.GetPage(1);
        double side = Math.Max(page.Width, page.Height);
        Assert.All(page.Letters, letter => Assert.InRange(letter.StartBaseLine.X, 0, side));
        Assert.All(page.Letters, letter => Assert.InRange(letter.StartBaseLine.Y, 0, side));
    }

    [Theory]
    [InlineData(200, 300, "", "")]
    [InlineData(200, 200, "", "1 0 0 1 0 50 cm")]
    [InlineData(200, 200, "/CropBox[50 50 150 150]", "1 0 0 1 0 50 cm")]
    [InlineData(400, 400, "", "0.5 0 0 0.5 0 50 cm")]
    public void AStampIsCentredOnThePageAndShrunkToFit(int width, int height, string extra, string placement)
    {
        byte[] stamp = Turned(width, height, 0, "Stamp", extra: extra);
        string content = FirstPageContent(PdfFile.Open(Turned(200, 300, 0, "XYZ")).Overlay(PdfFile.Open(stamp)).ToArray());

        Assert.Contains($"q\n{placement}{(placement.Length > 0 ? "\n" : string.Empty)}/Layer0 Do\nQ", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[0 0 200]", 0, false)]
    [InlineData("[0 0 /Wide 200]", 0, true)]
    [InlineData("[0 0 0 200]", 0, false)]
    [InlineData("7", 0, true)]
    [InlineData("[0 0 200 300]", 45, false)]
    public void AStampOnOrFromAPageOfNoSensibleBoxOrTurnIsDrawnWhereItIs(string box, int rotate, bool onTheStamp)
    {
        byte[] stamp = onTheStamp ? Turned(0, 0, rotate, "Stamp", box) : Turned(200, 300, 0, "Stamp");
        byte[] page = onTheStamp ? Turned(200, 300, 0, "XYZ") : Turned(0, 0, rotate, "XYZ", box);

        Assert.Contains("q\n/Layer0 Do\nQ", FirstPageContent(PdfFile.Open(page).Overlay(PdfFile.Open(stamp)).ToArray()), StringComparison.Ordinal);
    }

    /// <summary>The first page's content, its streams decoded and joined.</summary>
    private static string FirstPageContent(byte[] pdf)
    {
        PdfSource source = PdfSource.Open(pdf);
        PdfValue contents = source.Pages[0].Dictionary[PdfNames.Contents];
        IEnumerable<PdfValue> streams = source.Resolve(contents) is { Kind: PdfValueKind.Array } array ? array.AsArray().Cast<PdfValue>() : [contents];
        return string.Join("\n", streams.Select(stream => System.Text.Encoding.Latin1.GetString(source.Decode(source.Stream(stream)!))));
    }

    [Fact]
    public void WhatIsLaidOnATaggedPageIsAnArtifact()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Text("Tagged");
        }));
        byte[] tagged = document.ExportPdf(new PdfExportOptions { Tagged = true });

        string content = FirstPageContent(PdfFile.Open(tagged).Overlay(PdfFile.Open(Layer("Stamp"))).Underlay(PdfFile.Open(Layer("Head"))).ToArray());
        string plain = FirstPageContent(PdfFile.Open(Pages("One")).Overlay(PdfFile.Open(Layer("Stamp"))).ToArray());

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(content, @"/Artifact BMC\s+q\s+/Layer\d+ Do\s+Q\s+EMC").Count);
        Assert.Matches(@"q\s+/Layer\d+ Do\s+Q", plain);
        Assert.DoesNotContain("/Artifact", plain, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/MarkInfo<</Marked true>>", true)]
    [InlineData("/MarkInfo<</Marked false>>", false)]
    [InlineData("/MarkInfo<</Marked 1>>", false)]
    [InlineData("/MarkInfo<<>>", false)]
    [InlineData("/MarkInfo 7", false)]
    public void WhatIsLaidOnAFileMarkedAsTaggedIsAnArtifact(string catalog, bool artifact)
    {
        byte[] page = Turned(200, 200, 0, "XYZ", catalog: catalog);

        string content = FirstPageContent(PdfFile.Open(page).Overlay(PdfFile.Open(Turned(200, 200, 0, "Stamp"))).ToArray());

        Assert.Equal(artifact, content.IndexOf("/Artifact BMC\nq\n/Layer0 Do\nQ\nEMC", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public void AFileOptimizedForTheWebOpensWithItsFirstPageFirst()
    {
        byte[] linear = PdfFile.Open(Pages("One", "Two", "Three")).Overlay(PdfFile.Open(Layer("Stamp")), onto: "2").OptimizeForWeb().ToArray();
        string start = System.Text.Encoding.ASCII.GetString(linear, 0, 200);

        Assert.Matches(@"^%PDF-1\.7\n%....\n\d+ 0 obj\n<</Linearized 1/L 0*" + linear.Length + "/", start);
        Assert.Contains("/N 0000000003", start, StringComparison.Ordinal);
        Assert.Equal(["One", "Two Stamp", "Three"], Read(linear));

        using PdfDocument document = PdfDocument.Open(linear);
        Assert.True(document.TryGetBookmarks(out Bookmarks? bookmarks));
        Assert.Equal(3, bookmarks!.Roots.Count);
    }

    [Fact]
    public void AProtectedFileOptimizedForTheWebOpensWithItsPassword()
    {
        byte[] linear = PdfFile.Open(Pages("One", "Two"))
            .Protect(new Protection { UserPassword = "web", Encryption = EncryptionLevel.AesWith256Bits })
            .OptimizeForWeb()
            .ToArray();

        Assert.Throws<IncorrectPasswordException>(() => PdfFile.Open(linear));
        Assert.Equal(["One", "Two"], Read(PdfFile.Open(linear, "web").Unprotect().ToArray()));
    }

    [Fact]
    public void LayingAFileOfNoPagesChangesNothing()
    {
        string empty = "%PDF-1.7\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[]/Count 0>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF";
        PdfFile layer = PdfFile.Open(System.Text.Encoding.ASCII.GetBytes(empty));

        Assert.Equal(0, layer.PageCount);
        Assert.Equal(["One"], Read(PdfFile.Open(Pages("One")).Overlay(layer).ToArray()));
        Assert.Throws<InvalidOperationException>(() => layer.ToArray());
    }

    [Fact]
    public void AFileProtectedByAnotherSecurityHandlerIsNotOpened()
    {
        byte[] data = PdfFile.Open(Pages("One")).Protect(new Protection { OwnerPassword = "owner" }).ToArray();
        string text = System.Text.Encoding.Latin1.GetString(data);

        // Renamed in place, the same length, so that the file is otherwise sound.
        byte[] other = System.Text.Encoding.Latin1.GetBytes(text.Replace("/Filter/Standard", "/Filter/Standarx"));

        Assert.NotEqual(text, System.Text.Encoding.Latin1.GetString(other));
        Assert.Throws<NotSupportedException>(() => PdfFile.Open(other));
    }

    [Fact]
    public void AFileWhoseKeyIsLostIsUnreadable()
    {
        byte[] data = PdfFile.Open(Pages("One")).Protect(new Protection { OwnerPassword = "owner", Encryption = EncryptionLevel.AesWith256Bits }).ToArray();
        string text = System.Text.Encoding.Latin1.GetString(data);

        // Renamed in place, as the handler is above: the owner's password is right, but the key it unwraps is gone.
        byte[] lost = System.Text.Encoding.Latin1.GetBytes(text.Replace("/OE<", "/OX<"));

        Assert.NotEqual(text, System.Text.Encoding.Latin1.GetString(lost));
        Assert.Throws<UnreadableFileException>(() => PdfFile.Open(lost, "owner"));
    }

    [Fact]
    public void AFileOfNoPagesCannotBeSaved()
    {
        PdfFile file = PdfFile.Open(Pages("One"));

        Assert.Throws<ArgumentException>(() => file.KeepPages(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => file.KeepPages("2"));
        Assert.Equal(1, file.PageCount);
    }

    [Fact]
    public void FilesAreOpenedAndSavedAsFilesAndStreams()
    {
        string directory = Path.Combine(Path.GetTempPath(), "pdffile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string first = Path.Combine(directory, "first.pdf");
            string second = Path.Combine(directory, "second.pdf");
            File.WriteAllBytes(first, Pages("One"));
            File.WriteAllBytes(second, Pages("Two"));
            string layer = Path.Combine(directory, "layer.pdf");
            File.WriteAllBytes(layer, Layer("Laid"));

            // Saving over the file opened replaces it only once the new one is complete.
            PdfFile.Open(first).Append(second).Overlay(layer, onto: "1").Underlay(layer, onto: "1").Save(first);

            using FileStream stream = File.OpenRead(first);
            PdfFile reopened = PdfFile.Open(stream);

            Assert.Equal(2, reopened.PageCount);
            Assert.Equal(["LaidOneLaid", "Two"], Drawn(File.ReadAllBytes(first)));
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WhatIsNotAPdfIsRefused() =>
        Assert.Throws<UnreadableFileException>(() => PdfFile.Open("not a pdf"u8.ToArray()));

    [Fact]
    public void NothingIsRequiredThatIsNotGiven()
    {
        PdfFile file = PdfFile.Open(Pages("One"));

        Assert.Throws<ArgumentNullException>(() => PdfFile.Open((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => PdfFile.Open((Stream)null!));
        Assert.ThrowsAny<ArgumentException>(() => PdfFile.Open((string)null!));
        Assert.Throws<ArgumentNullException>(() => file.KeepPages(null!));
        Assert.Throws<ArgumentNullException>(() => file.Append((PdfFile)null!));
        Assert.Throws<ArgumentNullException>(() => file.Overlay((PdfFile)null!));
        Assert.Throws<ArgumentNullException>(() => file.Save((Stream)null!));
        Assert.ThrowsAny<ArgumentException>(() => file.Save(string.Empty));
    }
}
