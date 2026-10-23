namespace Rustaveli.Pdf.Output;

/// <summary>
/// Writes a file beside its destination and moves it into place once complete, so a failed write leaves whatever was
/// at the destination untouched rather than a truncated file.
/// </summary>
/// <remarks>
/// On Windows a file another process holds open cannot be replaced or moved, and a virus scanner opens a file as soon
/// as the handle that wrote it closes. The move is tried again for a moment on the errors such a hold gives, and on no
/// other: a locked file, or any other failure, fails at once.
/// </remarks>
internal static class FileOutput
{
    private const int Attempts = 20;

    private const int SharingViolation = unchecked((int)0x80070020);

    private const int UnableToRemoveReplaced = unchecked((int)0x80070497);

    private const int UnableToMoveReplacement = unchecked((int)0x80070498);

    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(50);

    public static void Write(string path, Action<Stream> write)
    {
        string target = Path.GetFullPath(path);
        string partial = target + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".partial";

        try
        {
            using (FileStream stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                write(stream);

            MoveIntoPlace(partial, target);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    internal static void MoveIntoPlace(string partial, string target)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(target))
                    File.Replace(partial, target, null);
                else
                    File.Move(partial, target);

                return;
            }
            catch (IOException exception) when (attempt < Attempts && IsHeld(exception))
            {
                Thread.Sleep(Pause);
            }
        }
    }

    /// <summary>Whether the failure is one another process holding the file open gives.</summary>
    internal static bool IsHeld(IOException exception) =>
        exception.HResult is SharingViolation or UnableToRemoveReplaced or UnableToMoveReplacement;
}
