namespace Rustaveli.Pdf;

/// <summary>
/// Shows documents in the browser while they are written. Run the program under <c>dotnet watch</c> and every saved
/// change to the code that composes the document redraws it, without restarting.
/// </summary>
public static class DocumentPreview
{
    /// <summary>
    /// Shows the document <paramref name="compose"/> makes, composing it again on every change, until the process is
    /// stopped — as a program written only to preview a document would. Blocks.
    /// </summary>
    public static void Preview(Func<Document> compose, PreviewOptions? options = null) =>
        Preview(compose, options, Console.Out, WhenCtrlC);

    /// <summary>
    /// Shows the document until <paramref name="whenStopped"/> calls the action it is handed, saying where on
    /// <paramref name="output"/>. What <paramref name="whenStopped"/> returns is disposed once the preview stops.
    /// </summary>
    internal static void Preview(Func<Document> compose, PreviewOptions? options, TextWriter output, Func<Action, IDisposable> whenStopped)
    {
        using PreviewSession session = StartPreview(compose, options);
        using ManualResetEventSlim stopped = new ManualResetEventSlim();
        using IDisposable stopping = whenStopped(stopped.Set);

        output.WriteLine($"Previewing at {session.Url} — press Ctrl+C to stop.");
        stopped.Wait();
    }

    /// <summary>Starts showing the document <paramref name="compose"/> makes, returning the session that serves it.</summary>
    public static PreviewSession StartPreview(Func<Document> compose, PreviewOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compose);

        options ??= new PreviewOptions();
        PreviewSession session = new PreviewSession(compose, options);

        if (options.OpenBrowser)
            PdfExport.Open(session.Url.ToString());

        return session;
    }

    /// <summary>Calls <paramref name="stop"/> when Ctrl+C is pressed, instead of ending the process, until disposed.</summary>
    private static IDisposable WhenCtrlC(Action stop)
    {
        ConsoleCancelEventHandler handler = (_, arguments) =>
        {
            arguments.Cancel = true;
            stop();
        };

        Console.CancelKeyPress += handler;
        return new Subscription(() => Console.CancelKeyPress -= handler);
    }

    /// <summary>Undoes a subscription when disposed.</summary>
    private sealed class Subscription(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
