using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The entry points that turn a document into a file: their overloads, options, metadata and serialisation.
/// </summary>
public class PdfExportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static Document TextDocument(string text = "Generated") =>
        Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 200);
            page.Margins = Sides.All(10);
            page.Body().Text(text);
        }));

    /// <summary>A document whose drawing fails part-way through its only page.</summary>
    private static Document FailingDocument() =>
        Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 200);
            page.Body().Image(new ForeignImage());
        }));

    /// <summary>
    /// A document with no text, so renders overlapping it cannot interfere through Skia's process-wide font
    /// state, which is the hazard the render gate exists to prevent.
    /// </summary>
    private static Document ShapeDocument() =>
        Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 200);
            page.Body().Height(50).Placeholder(TestInks.Red);
        }));

    /// <summary>
    /// A document whose layout signals <paramref name="entered"/> and then waits for <paramref name="release"/>,
    /// pausing its render at a point where any gate it passed through is held.
    /// </summary>
    private static Document PausingDocument(ManualResetEventSlim entered, ManualResetEventSlim release) =>
        Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 200);
            page.Body()
                .DefaultType(style =>
                {
                    entered.Set();
                    release.Wait();
                    return style;
                })
                .Height(50)
                .Placeholder(TestInks.Blue);
        }));

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"rustaveli-{Guid.NewGuid():N}.pdf");

    // ---- Overloads ---------------------------------------------------------------------------------------------

    [Fact]
    public void EveryOverloadRejectsAMissingDocument()
    {
        Document? missing = null;
        string path = TempPath();

        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.ExportPdf()).ParamName);
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.ExportPdf(new MemoryStream())).ParamName);
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.ExportPdf(path)).ParamName);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TheStreamOverloadRejectsAMissingStream()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => TextDocument().ExportPdf((Stream)null!));

        Assert.Equal("stream", error.ParamName);
    }

    [Fact]
    public void ThePathOverloadRejectsAMissingPath()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => TextDocument().ExportPdf((string)null!));

        Assert.Equal("path", error.ParamName);
    }

    [Fact]
    public void EveryOverloadProducesTheSameFile()
    {
        Document document = TextDocument();
        byte[] expected = document.ExportPdf();
        string path = TempPath();

        try
        {
            using MemoryStream stream = new MemoryStream();
            document.ExportPdf(stream);
            document.ExportPdf(path);

            Assert.Equal(expected, stream.ToArray());
            Assert.Equal(expected, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }

        using PdfDocument parsed = PdfDocument.Open(expected);
        Assert.Equal("Generated", parsed.GetPage(1).Text);
    }

#if NET
    [Fact]
    public void ReturningTheFileAsAnArrayCostsOneCopyOfIt()
    {
        // Allocation budget: what the array overload allocates beyond exporting the same document into a stream is the
        // array it returns, plus a little bookkeeping. A buffer grown by doubling and then copied cost about three
        // times the file, its larger steps on the large object heap.
        const long Bookkeeping = 1024;
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(300, 300);
            page.Margins = Sides.All(10);
            page.Body().Stack(stack =>
            {
                for (int paragraph = 0; paragraph < 200; paragraph++)
                    stack.Add().Text($"Paragraph {paragraph}: the words of a document long enough to span several pages.");
            });
        }));
        document.ExportPdf(Stream.Null);
        document.ExportPdf();

        long before = GC.GetAllocatedBytesForCurrentThread();
        document.ExportPdf(Stream.Null);
        long streamed = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        byte[] file = document.ExportPdf();
        long returned = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            returned - streamed <= file.Length + Bookkeeping,
            $"Returning the {file.Length:N0}-byte file allocated {returned - streamed:N0} bytes more than streaming it.");
    }
#endif

    [Fact]
    public void GeneratingToAStreamAppendsAtItsCurrentPosition()
    {
        Document document = TextDocument();
        byte[] expected = document.ExportPdf();
        using MemoryStream stream = new MemoryStream();
        stream.Write([1, 2, 3], 0, 3);

        document.ExportPdf(stream);

        Assert.Equal(new byte[] { 1, 2, 3 }.Concat(expected), stream.ToArray());
    }

    [Fact]
    public void GeneratingToAPathReplacesAnExistingFile()
    {
        Document document = TextDocument();
        string path = TempPath();

        try
        {
            File.WriteAllBytes(path, new byte[1_000_000]);

            document.ExportPdf(path);

            Assert.Equal(document.ExportPdf(), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFailedRenderLeavesTheTargetFileUntouched()
    {
        string path = TempPath();

        try
        {
            File.WriteAllText(path, "previous contents");

            RenderingException error = Assert.Throws<RenderingException>(() => FailingDocument().ExportPdf(path));

            Assert.Equal("image", Assert.IsType<ArgumentException>(error.InnerException).ParamName);
            Assert.Equal("previous contents", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFailedRenderFailsTheCallAndLeavesTheStreamOpen()
    {
        // Pages stream out as they finish, so a stream may hold part of a document; the caller learns of the
        // failure and still owns the stream.
        using MemoryStream stream = new MemoryStream();

        RenderingException error = Assert.Throws<RenderingException>(() => FailingDocument().ExportPdf(stream));

        Assert.IsType<ArgumentException>(error.InnerException);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void AStreamThatCannotBeWrittenIsRefusedBeforeAnythingIsSet()
    {
        using MemoryStream readOnly = new MemoryStream(new byte[16], writable: false);

        ArgumentException error = Assert.Throws<ArgumentException>(() => TextDocument().ExportPdf(readOnly));

        Assert.Equal("stream", error.ParamName);
    }

    [Fact]
    public void RendersIntoAStreamThatCannotSeek()
    {
        // Regression: Skia asked the stream for its Position from native code. A stream that cannot answer — a
        // response body, a compression or network stream — threw there, where an exception cannot unwind, and
        // took the whole process down with an access violation.
        Document document = TextDocument();
        using MemoryStream received = new MemoryStream();

        document.ExportPdf(new ForwardOnlyStream(received));

        Assert.Equal(document.ExportPdf(), received.ToArray());
    }

    [Fact]
    public void AStreamThatRefusesTheWriteFailsTheCallRatherThanTheProcess()
    {
        // Regression: the same native-callback path turned a stream that throws on Write into a hung process.
        using MemoryStream full = new MemoryStream(new byte[64]);

        Assert.Throws<NotSupportedException>(() => TextDocument().ExportPdf(full));
    }

    // ---- Options -----------------------------------------------------------------------------------------------

    [Fact]
    public void NoOptionsMeansTheDefaultOptions()
    {
        Document document = TextDocument();

        Assert.Equal(document.ExportPdf(new PdfExportOptions()), document.ExportPdf());
    }

    [Fact]
    public void ByDefaultNoConformanceIsClaimed()
    {
        using PdfDocument parsed = PdfDocument.Open(TextDocument().ExportPdf());

        Assert.False(parsed.TryGetXmpMetadata(out _));
        Assert.False(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("OutputIntents")));
    }

    /// <summary>Awaits <paramref name="task"/>, failing with <paramref name="failure"/> if it has not finished in time.</summary>
    private static async Task<T> Within<T>(TimeSpan limit, Task<T> task, string failure)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(limit));

        Assert.True(finished == task, failure);
        return await task;
    }

    [Fact]
    public async Task ExportsRunAlongsideOneAnother()
    {
        // Each export has its own writer and shares only the typefaces, so nothing makes one wait for another.
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();

        Task<byte[]> holder = Task.Run(() => PausingDocument(entered, release).ExportPdf());
        byte[] concurrent;

        try
        {
            Assert.True(entered.Wait(Timeout), "The first export never reached its layout.");

            concurrent = await Within(
                Timeout,
                Task.Run(() => ShapeDocument().ExportPdf()),
                "A second export waited for the first.");
        }
        finally
        {
            release.Set();
        }

        using PdfDocument held = PdfDocument.Open(await Within(Timeout, holder, "The first export did not finish once released."));
        using PdfDocument parsed = PdfDocument.Open(concurrent);

        Assert.Equal(1, held.NumberOfPages);
        Assert.Equal(1, parsed.NumberOfPages);
    }

    // ---- Metadata ----------------------------------------------------------------------------------------------

    [Fact]
    public void WritesEveryDescriptiveField()
    {
        Document document = TextDocument();
        document.Info.Title = "Quarterly Statement";
        document.Info.Author = "Accounts";
        document.Info.Subject = "Balances for the quarter";
        document.Info.Keywords = "statement, quarter";
        document.Info.Creator = "Ledger";
        document.Info.Producer = "Ledger PDF";

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        DocumentInformation information = parsed.Information;

        Assert.Equal("Quarterly Statement", information.Title);
        Assert.Equal("Accounts", information.Author);
        Assert.Equal("Balances for the quarter", information.Subject);
        Assert.Equal("statement, quarter", information.Keywords);
        Assert.Equal("Ledger", information.Creator);
        Assert.Equal("Ledger PDF", information.Producer);
    }

    [Fact]
    public void NamesThisLibraryAsProducerUnlessToldOtherwise()
    {
        using PdfDocument parsed = PdfDocument.Open(TextDocument().ExportPdf());

        Assert.Equal("Rustaveli.Pdf", parsed.Information.Producer);
    }

    [Fact]
    public void OmitsFieldsThatWereNotSet()
    {
        Document document = TextDocument();
        document.Info.Producer = null;

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        DocumentInformation information = parsed.Information;

        Assert.Null(information.Title);
        Assert.Null(information.Author);
        Assert.Null(information.Subject);
        Assert.Null(information.Keywords);
        Assert.Null(information.Creator);
        Assert.Null(information.Producer);
        Assert.Null(information.CreationDate);
        Assert.Null(information.ModifiedDate);
    }

    [Fact]
    public void RecordsTheCreationAndModificationInstants()
    {
        // Regression: the dates were handed to SkiaSharp as UTC readings, but SkiaSharp stamps the machine's own
        // offset onto whatever reading it is given, so on any machine not set to UTC every date was shifted by
        // that offset.
        //
        // SkiaSharp also derives the offset from the difference between the local and UTC hour fields, which
        // produces nonsense when the two fall on different calendar days, and it writes whole hours only. The
        // instant is therefore centred on the local day, and a zone with a part-hour offset is allowed that part.
        DateTimeOffset noon = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        TimeSpan localOffset = TimeZoneInfo.Local.GetUtcOffset(noon);
        DateTimeOffset created = noon.AddTicks(-localOffset.Ticks / 2).ToOffset(TimeSpan.FromHours(2));
        DateTimeOffset modified = created.AddHours(3).AddMinutes(17);
        double tolerance = Math.Abs(localOffset.Minutes);

        Document document = TextDocument();
        document.Info.CreationDate = created;
        document.Info.ModificationDate = modified;

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        DateTimeOffset? writtenCreated = parsed.Information.GetCreatedDateTimeOffset();
        DateTimeOffset? writtenModified = parsed.Information.GetModifiedDateTimeOffset();

        Assert.True(writtenCreated.HasValue, $"Unreadable creation date '{parsed.Information.CreationDate}'.");
        Assert.True(writtenModified.HasValue, $"Unreadable modification date '{parsed.Information.ModifiedDate}'.");
        Assert.InRange(Math.Abs((writtenCreated!.Value - created).TotalMinutes), 0, tolerance);
        Assert.InRange(Math.Abs((writtenModified!.Value - modified).TotalMinutes), 0, tolerance);
    }

    [Fact]
    public void FontsAreEmbeddedWithoutHintingUnlessAskedToKeepIt()
    {
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            page.Body().Text("Hello, world");
        }));

        byte[] unhinted = document.ExportPdf();
        byte[] hinted = document.ExportPdf(new PdfExportOptions { KeepFontHinting = true });

        // Uncompressed, the font program lies in the file as it is, its table directory naming its tables.
        byte[] fpgm = "fpgm"u8.ToArray();
        Assert.False(Contains(document.ExportPdf(new PdfExportOptions { Compress = false }), fpgm));
        Assert.True(Contains(document.ExportPdf(new PdfExportOptions { Compress = false, KeepFontHinting = true }), fpgm));

        Assert.False(new PdfExportOptions().KeepFontHinting);
        Assert.True(unhinted.Length < hinted.Length * 0.7, $"{unhinted.Length} bytes unhinted against {hinted.Length} hinted.");

        // Either way the text is the same text.
        using PdfDocument read = PdfDocument.Open(unhinted);
        Assert.Equal("Hello, world", read.GetPage(1).Text);
    }

    private static bool Contains(byte[] data, byte[] part) => data.AsSpan().IndexOf(part) >= 0;
}
