namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// A PDF file opened, repaired where it can be, and written out again, plainly and linearised for the web. The only
/// acceptable failures are those <see cref="PdfFile.Open(byte[], string?)"/> documents, and a file of no pages is only
/// opened; the files
/// written are this library's own output, and must open and save again without any exception at all.
/// </summary>
internal static class PdfTarget
{
    public static void Run(ReadOnlySpan<byte> input)
    {
        byte[] saved;
        byte[] linearised;

        try
        {
            PdfFile file = PdfFile.Open(input.ToArray());

            // A file of no pages opens, to be laid over another, but is not saved: that throws, as it is documented to.
            if (file.PageCount == 0)
                return;

            saved = file.ToArray();
            linearised = PdfFile.Open(input.ToArray()).OptimizeForWeb().ToArray();
        }
        catch (Exception exception) when (exception is UnreadableFileException or IncorrectPasswordException or NotSupportedException)
        {
            return;
        }

        _ = PdfFile.Open(saved).ToArray();
        _ = PdfFile.Open(linearised).ToArray();
    }
}
