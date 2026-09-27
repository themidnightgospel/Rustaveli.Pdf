namespace Rustaveli.Pdf;

/// <summary>
/// Descriptive information written into the PDF's metadata dictionary.
/// </summary>
public sealed class DocumentInfo
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    /// <summary>
    /// The language the document is written in, as a BCP 47 tag such as <c>en-GB</c> or <c>ka</c>: what screen
    /// readers speak it in, and what accessible PDF requires.
    /// </summary>
    public string? Language { get; set; }

    public string? Creator { get; set; }

    public string? Producer { get; set; } = "Rustaveli.Pdf";

    public DateTimeOffset? CreationDate { get; set; }

    public DateTimeOffset? ModificationDate { get; set; }
}
