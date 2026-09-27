namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>What is added to a file as it is saved, beyond its pages.</summary>
internal sealed class SaveSettings
{
    public List<FileAttachment> Attachments { get; } = [];

    /// <summary>XMP descriptions added to the file's metadata, in order.</summary>
    public List<string> Metadata { get; } = [];
}
