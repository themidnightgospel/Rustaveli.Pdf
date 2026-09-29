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
