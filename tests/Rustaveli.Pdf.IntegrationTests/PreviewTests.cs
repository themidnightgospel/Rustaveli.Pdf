using System.Net;
using System.Net.Sockets;
using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;
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

    [Fact]
    public async Task ServesEveryFrameDrawnOnAPageWithTheLineThatMadeIt()
    {
        using PreviewSession session = Start(() => Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(144, 72);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Inset(10).Named("Greeting").Text("Hello");
        })));

        string frames = await Get(session, "/frames/1");

        Assert.StartsWith("[{\"name\":", frames, StringComparison.Ordinal);
        Assert.Matches("\"name\":\"\\\\\"Greeting\\\\\"\",\"source\":\"[^\"]*PreviewTests\\.cs:\\d+\",\"x\":10,\"y\":10,\"width\":124,\"height\":52,\"children\":\\[\\{", frames);
    }

    [Fact]
    public void FramesAreWrittenAsNestedJson()
    {
        LayoutInspection inspection = new LayoutInspection();
        inspection.BeginPage();
        LayoutInspection.Node node = inspection.Enter(new StackBlock(), new Offset(1.234f, 2), new Extent(3, 4));
        inspection.Leave(inspection.Enter(new NewPageBlock(), Offset.Zero, Extent.Zero));
        inspection.Leave(node);
        inspection.Leave(inspection.Enter(new NewPageBlock(), new Offset(5, 6), new Extent(7, 8)));

        Assert.Equal(
            "[{\"name\":\"Stack\",\"source\":null,\"x\":1.23,\"y\":2,\"width\":3,\"height\":4,\"children\":"
            + "[{\"name\":\"NewPage\",\"source\":null,\"x\":0,\"y\":0,\"width\":0,\"height\":0,\"children\":[]}]},"
            + "{\"name\":\"NewPage\",\"source\":null,\"x\":5,\"y\":6,\"width\":7,\"height\":8,\"children\":[]}]",
            PreviewSession.Frames(inspection.Pages[0]));
    }

    [Fact]
    public async Task EachAnswerSaysWhatItIsAndIsNeverCached()
    {
        using PreviewSession session = Start(() => Pages(2));

        foreach ((string path, string type) in new[]
        {
            ("/", "text/html; charset=utf-8"),
            ("/state", "application/json"),
            ("/pages/1", "image/png"),
            ("/frames/1", "application/json"),
            ("/frames/2", "application/json"),
            ("/elsewhere", "text/plain"),
        })
        {
            using HttpResponseMessage response = await Client.GetAsync(new Uri(session.Url, path));

            Assert.Equal(type, response.Content.Headers.ContentType!.ToString());
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        }

        using HttpResponseMessage missing = await Client.GetAsync(new Uri(session.Url, "/pages/9"));
        Assert.Equal("Not found", await missing.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/pages/12", "/pages/", 12)]
    [InlineData("/frames/1", "/frames/", 1)]
    [InlineData("/pages/", "/pages/", null)]
    [InlineData("/pages/-1", "/pages/", null)]
    [InlineData("/frames/1", "/pages/", null)]
    [InlineData("/pagesx/1", "/pages/", null)]
    public void APageNumberIsReadAfterItsPrefix(string path, string prefix, int? number) =>
        Assert.Equal(number, PreviewSession.PageNumber(path, prefix));

    [Fact]
    public async Task APreviewRunsUntilItIsToldToStop()
    {
        StringWriter output = new StringWriter();
        Action? stop = null;
        bool undone = false;
        Thread previewing = new Thread(() => DocumentPreview.Preview(
            () => Pages(1),
            new PreviewOptions { OpenBrowser = false },
            output,
            action =>
            {
                stop = action;
                return new Undo(() => undone = true);
            }));

        previewing.Start();
        SpinWait.SpinUntil(() => output.ToString().Contains("Previewing at", StringComparison.Ordinal), TimeSpan.FromSeconds(30));

        string said = output.ToString();
        Uri url = new Uri(said.Substring("Previewing at ".Length, said.IndexOf(" — ", StringComparison.Ordinal) - "Previewing at ".Length));
        Assert.EndsWith("press Ctrl+C to stop." + Environment.NewLine, said, StringComparison.Ordinal);
        Assert.Contains("\"version\":1", await Client.GetStringAsync(new Uri(url, "/state")), StringComparison.Ordinal);
        Assert.True(previewing.IsAlive);
        Assert.False(undone);

        stop!();

        Assert.True(previewing.Join(TimeSpan.FromSeconds(30)));
        Assert.True(undone);
        await Assert.ThrowsAnyAsync<Exception>(() => Client.GetStringAsync(new Uri(url, "/state")));
    }

    [Fact]
    public void ADisposedPreviewIsNoLongerRefreshedWhenCodeChanges()
    {
        using PreviewSession open = Start(() => Pages(1));
        PreviewSession closed = Start(() => Pages(1));
        closed.Dispose();

        Rustaveli.Pdf.Preview.HotReload.UpdateApplication(null);

        Assert.Equal(1, closed.Version);
        Assert.True(open.Version >= 2);
    }

    [Theory]
    [InlineData("/pages/0")]
    [InlineData("/pages/3")]
    [InlineData("/pages/x")]
    [InlineData("/frames/0")]
    [InlineData("/frames/3")]
    [InlineData("/frames/x")]
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
    public void AnImageTooShortForAHeaderHasNoSize() => Assert.Equal((0, 0), PreviewSession.PngSize(new byte[23]));

    [Fact]
    public void AnImageSizeIsReadFromEveryByteOfItsHeader()
    {
        byte[] header = new byte[24];
        header[16] = 0x01;
        header[17] = 0x02;
        header[18] = 0x03;
        header[19] = 0x04;
        header[20] = 0x05;
        header[21] = 0x06;
        header[22] = 0x07;
        header[23] = 0x08;

        Assert.Equal((0x01020304, 0x05060708), PreviewSession.PngSize(header));
    }

    [Fact]
    public void OptionsHaveSensibleDefaultsAndRefuseNonsense()
    {
        PreviewOptions options = new PreviewOptions();

        Assert.Equal(0, options.Port);
        Assert.Equal(144f, options.Resolution);
        Assert.True(options.OpenBrowser);
        Assert.Null(options.Typefaces);
        Assert.Contains("pixels per inch", Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolution = 0).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolution = float.PositiveInfinity);
        Assert.Throws<ArgumentNullException>(() => DocumentPreview.StartPreview(null!));
    }

    [Fact]
    public async Task AGivenPortIsUsed()
    {
        // A port the system has just handed out is free, and never one it reserves — as Windows reserves ranges
        // for Hyper-V — where a port picked at random might be.
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        using PreviewSession session = DocumentPreview.StartPreview(() => Pages(1), new PreviewOptions { OpenBrowser = false, Port = port });

        Assert.Equal(port, session.Url.Port);
        Assert.Contains("\"version\":1", await Get(session, "/state"), StringComparison.Ordinal);
    }

    /// <summary>Runs an action when disposed.</summary>
    private sealed class Undo(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
