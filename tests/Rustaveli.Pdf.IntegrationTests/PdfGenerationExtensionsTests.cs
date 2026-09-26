using System.Xml.Linq;
using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The entry points that turn a document into a file: their overloads, options, metadata and serialisation.
/// </summary>
public class PdfGenerationExtensionsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static Document TextDocument(string text = "Generated") =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 200);
            page.Margin = Edges.All(10);
            page.Content().Text(text);
        }));

    /// <summary>A document whose drawing fails part-way through its only page.</summary>
    private static Document FailingDocument() =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 200);
            page.Content().Image(new ForeignImage());
        }));

    /// <summary>
    /// A document with no text, so renders overlapping it cannot interfere through Skia's process-wide font
    /// state, which is the hazard the render gate exists to prevent.
    /// </summary>
    private static Document ShapeDocument() =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 200);
            page.Content().Height(50).Placeholder(Colors.Red);
        }));

    /// <summary>
    /// A document whose layout signals <paramref name="entered"/> and then waits for <paramref name="release"/>,
    /// pausing its render at a point where any gate it passed through is held.
    /// </summary>
    private static Document PausingDocument(ManualResetEventSlim entered, ManualResetEventSlim release) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 200);
            page.Content()
                .DefaultTextStyle(style =>
                {
                    entered.Set();
                    release.Wait();
                    return style;
                })
                .Height(50)
                .Placeholder(Colors.Blue);
        }));

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"rustaveli-{Guid.NewGuid():N}.pdf");

    // ---- Overloads ---------------------------------------------------------------------------------------------

    [Fact]
    public void EveryOverloadRejectsAMissingDocument()
    {
        Document? missing = null;
        string path = TempPath();

        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.GeneratePdf()).ParamName);
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.GeneratePdf(new MemoryStream())).ParamName);
        Assert.Equal("document", Assert.Throws<ArgumentNullException>(() => missing!.GeneratePdf(path)).ParamName);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TheStreamOverloadRejectsAMissingStream()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => TextDocument().GeneratePdf((Stream)null!));

        Assert.Equal("stream", error.ParamName);
    }

    [Fact]
    public void ThePathOverloadRejectsAMissingPath()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => TextDocument().GeneratePdf((string)null!));

        Assert.Equal("path", error.ParamName);
    }

    [Fact]
    public void EveryOverloadProducesTheSameFile()
    {
        Document document = TextDocument();
        byte[] expected = document.GeneratePdf();
        string path = TempPath();

        try
        {
            using MemoryStream stream = new MemoryStream();
            document.GeneratePdf(stream);
            document.GeneratePdf(path);

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

    [Fact]
    public void GeneratingToAStreamAppendsAtItsCurrentPosition()
    {
        Document document = TextDocument();
        byte[] expected = document.GeneratePdf();
        using MemoryStream stream = new MemoryStream();
        stream.Write([1, 2, 3], 0, 3);

        document.GeneratePdf(stream);

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

            document.GeneratePdf(path);

            Assert.Equal(document.GeneratePdf(), File.ReadAllBytes(path));
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

            DocumentDrawingException error = Assert.Throws<DocumentDrawingException>(() => FailingDocument().GeneratePdf(path));

            Assert.Equal("image", Assert.IsType<ArgumentException>(error.InnerException).ParamName);
            Assert.Equal("previous contents", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFailedRenderWritesNothingToTheStream()
    {
        using MemoryStream stream = new MemoryStream();

        DocumentDrawingException error = Assert.Throws<DocumentDrawingException>(() => FailingDocument().GeneratePdf(stream));

        Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void RendersIntoAStreamThatCannotSeek()
    {
        // Regression: Skia asked the stream for its Position from native code. A stream that cannot answer — a
        // response body, a compression or network stream — threw there, where an exception cannot unwind, and
        // took the whole process down with an access violation.
        Document document = TextDocument();
        using MemoryStream received = new MemoryStream();

        document.GeneratePdf(new ForwardOnlyStream(received));

        Assert.Equal(document.GeneratePdf(), received.ToArray());
    }

    [Fact]
    public void AStreamThatRefusesTheWriteFailsTheCallRatherThanTheProcess()
    {
        // Regression: the same native-callback path turned a stream that throws on Write into a hung process.
        using MemoryStream full = new MemoryStream(new byte[64]);

        Assert.Throws<NotSupportedException>(() => TextDocument().GeneratePdf(full));
    }

    // ---- Options -----------------------------------------------------------------------------------------------

    [Fact]
    public void NoOptionsMeansTheDefaultOptions()
    {
        Document document = TextDocument();

        Assert.Equal(document.GeneratePdf(new PdfGenerationOptions()), document.GeneratePdf());
    }

    [Fact]
    public void PdfAEmbedsTheConformanceClaim()
    {
        using PdfDocument parsed = PdfDocument.Open(TextDocument().GeneratePdf(new PdfGenerationOptions { PdfA = true }));

        Assert.True(parsed.TryGetXmpMetadata(out XmpMetadata? xmp), "PDF/A requires XMP metadata.");

        XDocument metadata = xmp!.GetXDocument();
        XNamespace pdfaid = "http://www.aiim.org/pdfa/ns/id/";

        Assert.Equal("2", metadata.Descendants(pdfaid + "part").Single().Value);
        Assert.Equal("B", metadata.Descendants(pdfaid + "conformance").Single().Value);
        Assert.True(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("OutputIntents")));
    }

    [Fact]
    public void ByDefaultNoConformanceIsClaimed()
    {
        using PdfDocument parsed = PdfDocument.Open(TextDocument().GeneratePdf());

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
    public async Task RendersQueueBehindOneAnotherByDefault()
    {
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();

        Task<byte[]> holder = Task.Run(() => PausingDocument(entered, release).GeneratePdf());
        Task<byte[]>? queued = null;

        try
        {
            Assert.True(entered.Wait(Timeout), "The first render never reached its layout.");

            queued = Task.Run(() => ShapeDocument().GeneratePdf());

            // Nothing but the gate holds the second render back, so it must still be waiting.
            Task first = await Task.WhenAny(queued, Task.Delay(TimeSpan.FromMilliseconds(500)));
            Assert.False(first == queued, "A second render ran while the first held the gate.");
        }
        finally
        {
            release.Set();
        }

        using PdfDocument held = PdfDocument.Open(await Within(Timeout, holder, "The first render did not finish once released."));
        using PdfDocument waited = PdfDocument.Open(await Within(Timeout, queued!, "The queued render did not run once the gate was free."));

        Assert.Equal(1, held.NumberOfPages);
        Assert.Equal(1, waited.NumberOfPages);
    }

    [Fact]
    public async Task AllowingConcurrentRenderingSkipsTheQueue()
    {
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        using ManualResetEventSlim release = new ManualResetEventSlim();

        Task<byte[]> holder = Task.Run(() => PausingDocument(entered, release).GeneratePdf());
        byte[] concurrent;

        try
        {
            Assert.True(entered.Wait(Timeout), "The first render never reached its layout.");

            concurrent = await Within(
                Timeout,
                Task.Run(() => ShapeDocument().GeneratePdf(new PdfGenerationOptions { AllowConcurrentRendering = true })),
                "A render that opted out of the gate still waited for it.");
        }
        finally
        {
            release.Set();
        }

        await Within(Timeout, holder, "The first render did not finish once released.");

        using PdfDocument parsed = PdfDocument.Open(concurrent);
        Assert.Equal(1, parsed.NumberOfPages);
    }

    // ---- Metadata ----------------------------------------------------------------------------------------------

    [Fact]
    public void WritesEveryDescriptiveField()
    {
        Document document = TextDocument();
        document.Metadata.Title = "Quarterly Statement";
        document.Metadata.Author = "Accounts";
        document.Metadata.Subject = "Balances for the quarter";
        document.Metadata.Keywords = "statement, quarter";
        document.Metadata.Creator = "Ledger";
        document.Metadata.Producer = "Ledger PDF";

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
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
        using PdfDocument parsed = PdfDocument.Open(TextDocument().GeneratePdf());

        Assert.Equal("Rustaveli.Pdf", parsed.Information.Producer);
    }

    [Fact]
    public void OmitsFieldsThatWereNotSet()
    {
        Document document = TextDocument();
        document.Metadata.Producer = null;

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
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
        document.Metadata.CreationDate = created;
        document.Metadata.ModificationDate = modified;

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        DateTimeOffset? writtenCreated = parsed.Information.GetCreatedDateTimeOffset();
        DateTimeOffset? writtenModified = parsed.Information.GetModifiedDateTimeOffset();

        Assert.True(writtenCreated.HasValue, $"Unreadable creation date '{parsed.Information.CreationDate}'.");
        Assert.True(writtenModified.HasValue, $"Unreadable modification date '{parsed.Information.ModifiedDate}'.");
        Assert.InRange(Math.Abs((writtenCreated!.Value - created).TotalMinutes), 0, tolerance);
        Assert.InRange(Math.Abs((writtenModified!.Value - modified).TotalMinutes), 0, tolerance);
    }
}
