namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Thrown when font data is malformed, truncated or in a format this library does not read.
/// </summary>
/// <remarks>
/// Font files arrive from users and from whatever is installed on a host, so parsing is an attack surface: every
/// failure to make sense of the bytes surfaces as this one type, never as an index or overflow exception from deep
/// inside a parser, so a caller can reject a bad font without also swallowing genuine bugs.
/// </remarks>
internal sealed class FontFormatException : FormatException
{
    public FontFormatException(string message)
        : base(message)
    {
    }

    public FontFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The data ends before a structure it declares.</summary>
    internal static FontFormatException Truncated() =>
        new FontFormatException("The font data ends before a structure it declares.");
}
