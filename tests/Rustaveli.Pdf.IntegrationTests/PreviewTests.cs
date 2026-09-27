using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// A document shown in the browser as it is written: its pages served as images, drawn again when it changes, and a
/// layout failure shown in their place.
/// </summary>
public class PreviewTests
{
    private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private static Document Pages(int count, string label = "Page") => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(144, 72);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
        section.Body().Stack(stack =>
        {
            for (int page = 1; page <= count; page++)
            {
                if (page > 1)
                    stack.Add().NewPage();

                stack.Add().Text(label + " " + page);
            }
        });
    }));

    private static PreviewSession Start(Func<Document> compose) =>
        DocumentPreview.StartPreview(compose, new PreviewOptions { OpenBrowser = false, Resolution = 72 });

    private static Task<string> Get(PreviewSession session, string path) => Client.GetStringAsync(new Uri(session.Url, path));

    [Fact]
    public async Task ServesThePageItsPagesAndTheirState()
    {
        using PreviewSession session = Start(() => Pages(2));

        string page = await Get(session, "/");
        string state = await Get(session, "/state");
        byte[] image = await Client.GetByteArrayAsync(new Uri(session.Url, "/pages/2?v=1"));

        Assert.Contains("<title>Preview</title>", page, StringComparison.Ordinal);
        Assert.Equal("{\"version\":1,\"pages\":[[192,96],[192,96]],\"error\":null}", state);
        Assert.Equal<byte>([0x89, (byte)'P', (byte)'N', (byte)'G'], image.Take(4));
        Assert.Equal((144, 72), PreviewSession.PngSize(image));
        Assert.StartsWith("http://localhost:", session.Url.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefreshComposesTheDocumentAgain()
    {
        int composed = 0;
        using PreviewSession session = Start(() => Pages(++composed));

        Assert.Contains("\"pages\":[[192,96]]", await Get(session, "/state"), StringComparison.Ordinal);
        await Get(session, "/state");
        Assert.Equal(1, composed);

        session.Refresh();

        Assert.Equal(2, session.Version);
        Assert.Contains("\"version\":2,\"pages\":[[192,96],[192,96]]", await Get(session, "/state"), StringComparison.Ordinal);
        Assert.Equal(2, composed);
    }

    [Fact]
    public void AChangeOfCodeRefreshesEveryOpenPreview()
    {
        using PreviewSession first = Start(() => Pages(1));
        using PreviewSession second = Start(() => Pages(1));

        Rustaveli.Pdf.Preview.HotReload.ClearCache(null);
        Rustaveli.Pdf.Preview.HotReload.UpdateApplication(null);

        Assert.True(first.Version >= 3);
        Assert.True(second.Version >= 3);
        Assert.NotEqual(first.Url, second.Url);
    }

    [Fact]
    public async Task ALayoutFailureIsShownInPlaceOfThePages()
    {
        using PreviewSession session = Start(() => Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(100, 100);
            section.Body().Named("Too tall").Height(500).Blank();
        })));

        string state = await Get(session, "/state");

        Assert.StartsWith("{\"version\":1,\"pages\":[],\"error\":\"OversetException: The body cannot be set", state, StringComparison.Ordinal);
        Assert.Contains("\\n  \\\"Too tall\\\", offered 100 × 100: does not fit", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatTheComposingCodeThrowsIsShownWithWhatCausedIt()
    {
        using PreviewSession session = Start(() => throw new InvalidOperationException("Not written yet", new FormatException("bad date")));

        string state = await Get(session, "/state");

        Assert.Contains("\"error\":\"InvalidOperationException: Not written yet\\n\\nFormatException: bad date\"", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AComposeFunctionThatReturnsNoDocumentIsAFailure()
    {
        using PreviewSession session = Start(() => null!);

        Assert.Contains("returned no document", await Get(session, "/state"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/pages/0")]
    [InlineData("/pages/3")]
    [InlineData("/pages/x")]
    [InlineData("/elsewhere")]
    public async Task WhatIsNotThereIsNotFound(string path)
    {
        using PreviewSession session = Start(() => Pages(2));

        HttpResponseMessage response = await Client.GetAsync(new Uri(session.Url, path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ADisposedPreviewIsServedNoLonger()
    {
        PreviewSession session = Start(() => Pages(1));
        Uri url = session.Url;

        session.Dispose();
        session.Dispose();

        await Assert.ThrowsAnyAsync<Exception>(() => Client.GetStringAsync(new Uri(url, "/state")));
    }

    [Fact]
    public void StartingOpensTheBrowserUnlessToldNot()
    {
        List<string> opened = [];
        Action<string> open = PdfExport.Open;
        PdfExport.Open = opened.Add;

        try
        {
            using PreviewSession session = DocumentPreview.StartPreview(() => Pages(1));
            Assert.Equal([session.Url.ToString()], opened);
        }
        finally
        {
            PdfExport.Open = open;
        }
    }

    [Fact]
    public void JsonTextIsEscaped() =>
        Assert.Equal("\"q\\\"b\\\\n\\n\\r\\t\\u0001é\"", PreviewSession.Quote("q\"b\\n\n\r\t\u0001é"));

    [Fact]
    public void AnImageTooShortForAHeaderHasNoSize() => Assert.Equal((0, 0), PreviewSession.PngSize(new byte[10]));

    [Fact]
    public void OptionsHaveSensibleDefaultsAndRefuseNonsense()
    {
        PreviewOptions options = new PreviewOptions();

        Assert.Equal(0, options.Port);
        Assert.Equal(144f, options.Resolution);
        Assert.True(options.OpenBrowser);
        Assert.Null(options.Typefaces);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolution = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolution = float.PositiveInfinity);
        Assert.Throws<ArgumentNullException>(() => DocumentPreview.StartPreview(null!));
    }

    [Fact]
    public async Task AGivenPortIsUsed()
    {
        int port = new Random().Next(40_000, 50_000);
        using PreviewSession session = DocumentPreview.StartPreview(() => Pages(1), new PreviewOptions { OpenBrowser = false, Port = port });

        Assert.Equal(port, session.Url.Port);
        Assert.Contains("\"version\":1", await Get(session, "/state"), StringComparison.Ordinal);
    }
}
