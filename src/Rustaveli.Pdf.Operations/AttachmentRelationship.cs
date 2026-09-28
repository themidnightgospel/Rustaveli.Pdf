namespace Rustaveli.Pdf;

/// <summary>How an attached file relates to the document it is attached to (ISO 32000-2, 14.13.2).</summary>
public enum AttachmentRelationship
{
    /// <summary>The relationship is not known, or is none of the others.</summary>
    Unspecified,

    /// <summary>The file the document was made from, such as a word-processing document.</summary>
    Source,

    /// <summary>Data the document shows, such as the figures behind a chart.</summary>
    Data,

    /// <summary>The document itself in another form, such as the XML of an electronic invoice.</summary>
    Alternative,

    /// <summary>Something added to the document, such as a set of fonts it needs.</summary>
    Supplement,
}
