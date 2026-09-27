namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when a file cannot be read as a PDF: it is not one, or it is damaged beyond what a reader can repair.
/// </summary>
public sealed class UnreadableFileException : Exception
{
    public UnreadableFileException(string message)
        : base(message)
    {
    }

    public UnreadableFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
