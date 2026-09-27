namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// What the font's licence permits a document to do with it, from the <c>OS/2</c> fsType field.
/// </summary>
/// <remarks>
/// Reported, never enforced: whether to honour a restriction, warn, or substitute another font is the caller's
/// policy. A font without an <c>OS/2</c> table declares no restrictions. Old fonts sometimes set several of the
/// mutually exclusive usage bits; the specification says the least restrictive one then applies.
/// </remarks>
internal readonly struct FontEmbedding
{
    private const ushort RestrictedBit = 0x0002;
    private const ushort PreviewAndPrintBit = 0x0004;
    private const ushort EditableBit = 0x0008;
    private const ushort NoSubsettingBit = 0x0100;
    private const ushort BitmapOnlyBit = 0x0200;

    public FontEmbedding(ushort fsType)
    {
        FsType = fsType;
    }

    public ushort FsType { get; }

    /// <summary>Installable embedding: no restriction at all. Also the value for a font that says nothing.</summary>
    public bool IsInstallable => (FsType & (RestrictedBit | PreviewAndPrintBit | EditableBit)) == 0;

    /// <summary>The font must not be embedded in any form.</summary>
    public bool IsRestricted => (FsType & (RestrictedBit | PreviewAndPrintBit | EditableBit)) == RestrictedBit;

    /// <summary>May be embedded in a document that is only viewed and printed, which a generated PDF is.</summary>
    public bool IsPreviewAndPrint => (FsType & (PreviewAndPrintBit | EditableBit)) == PreviewAndPrintBit;

    /// <summary>May be embedded in a document that can be edited.</summary>
    public bool IsEditable => (FsType & EditableBit) != 0;

    /// <summary>The font may be embedded as a subset; when false, only the whole font may be.</summary>
    public bool AllowsSubsetting => (FsType & NoSubsettingBit) == 0;

    /// <summary>Only bitmaps may be embedded, never outlines — which rules out every PDF font file type.</summary>
    public bool IsBitmapOnly => (FsType & BitmapOnlyBit) != 0;

    /// <summary>The font's outlines may be embedded in a PDF at all.</summary>
    public bool AllowsEmbedding => !IsRestricted && !IsBitmapOnly;
}
