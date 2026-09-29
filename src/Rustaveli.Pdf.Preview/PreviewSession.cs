using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Preview;

namespace Rustaveli.Pdf;

/// <summary>
/// A document shown in the browser at <see cref="Url"/> as it is written: composed again and redrawn whenever
/// <see cref="Refresh"/> is called or code changes under <c>dotnet watch</c>, a layout failure shown on the page in
/// place of it, until the session is disposed.
/// </summary>
/// <remarks>
/// The preview is served on this machine only. Pages are drawn when the browser asks for them after a change, not as
/// the change happens, so a burst of edits costs one drawing.
/// </remarks>
public sealed class PreviewSession : IDisposable
{
    private static readonly List<WeakReference<PreviewSession>> Sessions = [];

    private readonly Func<Document> _compose;
    private readonly PreviewOptions _options;
    private readonly HttpListener _listener;
    private readonly object _drawing = new object();
    private int _version = 1;
    private int _drawnVersion;
    private List<byte[]> _pages = [];
    private LayoutInspection _frames = new LayoutInspection();
    private string? _error;
    private bool _disposed;

    internal PreviewSession(Func<Document> compose, PreviewOptions options)
    {
        _compose = compose;
        _options = options;

        int port = options.Port > 0 ? options.Port : FreePort();
        Url = new Uri($"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}/");

        _listener = new HttpListener();
        _listener.Prefixes.Add(Url.ToString());
        _listener.Start();

        lock (Sessions)
            Sessions.Add(new WeakReference<PreviewSession>(this));

        // A thread of its own, as a background thread, so a program that ends is not held open by it.
        Task.Factory.StartNew(Serve, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>Where the preview is served.</summary>
    public Uri Url { get; }

    /// <summary>How many times the document has been asked to be composed afresh, counting the first.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Composes the document again and redraws it the next time the browser looks.</summary>
    public void Refresh() => Interlocked.Increment(ref _version);

    /// <summary>Stops serving the preview.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _listener.Close();

        lock (Sessions)
            Sessions.RemoveAll(reference => !reference.TryGetTarget(out PreviewSession? session) || ReferenceEquals(session, this));
    }

    /// <summary>Refreshes every open session: code has changed.</summary>
    internal static void RefreshAll()
    {
        lock (Sessions)
        {
            foreach (WeakReference<PreviewSession> reference in Sessions)
            {
                if (reference.TryGetTarget(out PreviewSession? session))
                    session.Refresh();
            }
        }
    }

    /// <summary>
    /// The pages as drawn for the latest version with every frame drawn on them, or the failure that stopped them
    /// being drawn.
    /// </summary>
    internal (int Version, IReadOnlyList<byte[]> Pages, LayoutInspection Frames, string? Error) Draw()
    {
        lock (_drawing)
        {
            int version = Version;

            if (_drawnVersion != version)
            {
                _frames = new LayoutInspection();

                try
                {
                    Document document;

                    // Each element remembers the line that made it, so the inspector can lead back to it.
                    using (SourceCapture.Record())
                        document = _compose() ?? throw new InvalidOperationException("The preview's compose function returned no document.");

                    ImageExportOptions options = new ImageExportOptions { Resolution = _options.Resolution, Typefaces = _options.Typefaces };
                    _pages = ImageExport.ExportImages(document, options, _frames).ToList();
                    _error = null;
                }
                catch (Exception exception)
                {
                    // Whatever the document's code throws is what the writer needs to see, not a dead page.
                    _pages = [];
                    _frames = new LayoutInspection();
                    _error = Describe(exception);
                }

                _drawnVersion = version;
            }

            return (version, _pages, _frames, _error);
        }
    }

    private static string Describe(Exception exception)
    {
        StringBuilder text = new StringBuilder();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (text.Length > 0)
                text.Append("\n\n");

            text.Append(current.GetType().Name).Append(": ").Append(current.Message);
        }

        return text.ToString();
    }

    private static int FreePort()
    {
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private void Serve()
    {
        while (!_disposed)
        {
            HttpListenerContext context;

            try
            {
                context = _listener.GetContext();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ => Answer(context));
        }
    }

    private void Answer(HttpListenerContext context)
    {
        HttpListenerResponse response = context.Response;

        try
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";
            response.Headers["Cache-Control"] = "no-store";

            if (path == "/")
            {
                Send(response, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(PreviewPage.Html));
            }
            else if (path == "/state")
            {
                Send(response, "application/json", Encoding.UTF8.GetBytes(State()));
            }
            else if (path == "/icon.png")
            {
                Send(response, "image/png", PreviewPage.Icon);
            }
            else if (PageNumber(path, "/pages/") is int number && Draw().Pages is { } pages && number >= 1 && number <= pages.Count)
            {
                Send(response, "image/png", pages[number - 1]);
            }
            else if (PageNumber(path, "/frames/") is int page && Draw().Frames.Pages is { } drawn && page >= 1 && page <= drawn.Count)
            {
                Send(response, "application/json", Encoding.UTF8.GetBytes(Frames(drawn[page - 1])));
            }
            else
            {
                response.StatusCode = 404;
                Send(response, "text/plain", Encoding.UTF8.GetBytes("Not found"));
            }
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException)
        {
            // The browser went away mid-answer; there is no one to tell.
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                // Already gone.
            }
        }
    }

    /// <summary>The number after <paramref name="prefix"/> in <paramref name="path"/>, or null when there is none.</summary>
    internal static int? PageNumber(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.Ordinal)
        && int.TryParse(path.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : null;

    /// <summary>The state the page polls: the version, each page's size on screen, and any failure.</summary>
    private string State()
    {
        (int version, IReadOnlyList<byte[]> pages, _, string? error) = Draw();
        StringBuilder json = new StringBuilder("{\"version\":").Append(version.ToString(CultureInfo.InvariantCulture)).Append(",\"pages\":[");

        for (int index = 0; index < pages.Count; index++)
        {
            (int width, int height) = PngSize(pages[index]);
            float scale = 96f / _options.Resolution;

            if (index > 0)
                json.Append(',');

            json.Append('[').Append(Math.Round(width * scale).ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(Math.Round(height * scale).ToString(CultureInfo.InvariantCulture)).Append(']');
        }

        json.Append("],\"error\":").Append(error is null ? "null" : Quote(error)).Append('}');
        return json.ToString();
    }

    /// <summary>
    /// The frames drawn on a page as the inspector reads them: each one's name, the line that made it, its top left
    /// and its size in points, and the frames it drew within it.
    /// </summary>
    internal static string Frames(IReadOnlyList<LayoutInspection.Node> nodes)
    {
        StringBuilder json = new StringBuilder();
        Append(nodes);
        return json.ToString();

        void Append(IReadOnlyList<LayoutInspection.Node> level)
        {
            json.Append('[');

            for (int index = 0; index < level.Count; index++)
            {
                LayoutInspection.Node node = level[index];

                if (index > 0)
                    json.Append(',');

                json.Append("{\"name\":").Append(Quote(node.Name))
                    .Append(",\"source\":").Append(node.Source is null ? "null" : Quote(node.Source))
                    .Append(",\"x\":").Append(Number(node.Origin.X))
                    .Append(",\"y\":").Append(Number(node.Origin.Y))
                    .Append(",\"width\":").Append(Number(node.Size.Width))
                    .Append(",\"height\":").Append(Number(node.Size.Height))
                    .Append(",\"children\":");
                Append(node.Children);
                json.Append('}');
            }

            json.Append(']');
        }

        static string Number(float value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A PNG's size in pixels, from its header.</summary>
    internal static (int Width, int Height) PngSize(byte[] png) =>
        png.Length < 24 ? (0, 0) : ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    internal static string Quote(string text)
    {
        StringBuilder quoted = new StringBuilder("\"");

        foreach (char character in text)
        {
            switch (character)
            {
                case '"':
                    quoted.Append("\\\"");
                    break;
                case '\\':
                    quoted.Append("\\\\");
                    break;
                case '\n':
                    quoted.Append("\\n");
                    break;
                case '\r':
                    quoted.Append("\\r");
                    break;
                case '\t':
                    quoted.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                        quoted.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        quoted.Append(character);

                    break;
            }
        }

        return quoted.Append('"').ToString();
    }

    private static void Send(HttpListenerResponse response, string type, byte[] body)
    {
        response.ContentType = type;
        response.ContentLength64 = body.Length;
        response.OutputStream.Write(body, 0, body.Length);
    }
}
