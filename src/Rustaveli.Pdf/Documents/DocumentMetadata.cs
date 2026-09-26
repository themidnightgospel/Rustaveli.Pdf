namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Descriptive information written into the PDF's metadata dictionary.
/// </summary>
public sealed class DocumentMetadata
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    public string? Creator { get; set; }

    public string? Producer { get; set; } = "Rustaveli.Pdf";

    public DateTimeOffset? CreationDate { get; set; }

    public DateTimeOffset? ModificationDate { get; set; }
}
