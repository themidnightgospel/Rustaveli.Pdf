namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>What is added to a file as it is saved, beyond its pages.</summary>
internal sealed class SaveSettings
{
    public List<FileAttachment> Attachments { get; } = [];

    /// <summary>XMP descriptions added to the file's metadata, in order.</summary>
    public List<string> Metadata { get; } = [];

    /// <summary>Whether the first file's protection is kept, as it is unless replaced or removed.</summary>
    public bool KeepProtection { get; set; } = true;

    /// <summary>The protection the file is saved with in place of its own, if any.</summary>
    public Protection? Protection { get; set; }

    /// <summary>Whether the restrictions a signature places on the file, its <c>/Perms</c>, are dropped.</summary>
    public bool LiftRestrictions { get; set; }

    /// <summary>Whether the file is written for viewing over the web as it downloads.</summary>
    public bool Linearize { get; set; }
}
