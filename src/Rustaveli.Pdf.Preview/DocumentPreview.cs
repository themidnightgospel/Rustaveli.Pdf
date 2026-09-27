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
    public static void Preview(Func<Document> compose, PreviewOptions? options = null)
    {
        using PreviewSession session = StartPreview(compose, options);
        using ManualResetEventSlim stopped = new ManualResetEventSlim();

        Console.CancelKeyPress += (_, arguments) =>
        {
            arguments.Cancel = true;
            stopped.Set();
        };

        Console.WriteLine($"Previewing at {session.Url} — press Ctrl+C to stop.");
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
}
