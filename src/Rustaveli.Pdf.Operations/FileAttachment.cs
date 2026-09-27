namespace Rustaveli.Pdf;

/// <summary>
/// A file carried inside a PDF, listed among its attachments: the XML of an electronic invoice, a spreadsheet of the
/// figures a report shows, the source a drawing was made from.
/// </summary>
public sealed class FileAttachment
{
    /// <summary>An attachment of <paramref name="content"/>, listed as <paramref name="name"/>.</summary>
    public FileAttachment(string name, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);

        Name = name;
        Content = content;
    }

    /// <summary>The file's name as readers list it, such as <c>factur-x.xml</c>.</summary>
    public string Name { get; }

    public byte[] Content { get; }

    /// <summary>The file's media type, such as <c>text/xml</c>; PDF/A-3 requires one.</summary>
    public string? MediaType { get; init; }

    /// <summary>What the file is, as readers show it beside the name.</summary>
    public string? Description { get; init; }

    public DateTimeOffset? CreationDate { get; init; }

    /// <summary>When the file last changed; PDF/A-3 requires one, and the moment of saving stands in when none is given.</summary>
    public DateTimeOffset? ModificationDate { get; init; }

    /// <summary>How the file relates to the document, as PDF/A-3 records it.</summary>
    public AttachmentRelationship Relationship { get; init; } = AttachmentRelationship.Unspecified;

    /// <summary>An attachment of the file at <paramref name="path"/>, listed by its file name.</summary>
    public static FileAttachment FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new FileAttachment(Path.GetFileName(path), File.ReadAllBytes(path))
        {
            ModificationDate = File.GetLastWriteTimeUtc(path),
        };
    }
}
