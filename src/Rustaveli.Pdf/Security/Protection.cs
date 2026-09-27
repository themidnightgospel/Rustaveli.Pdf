namespace Rustaveli.Pdf;

/// <summary>
/// How a PDF is protected by password: what opens it, what lifts its restrictions, what a reader may do with it once
/// open, and how strongly it is encrypted.
/// </summary>
/// <remarks>
/// Restrictions are honoured by readers, not enforced by the encryption: anyone who can open a file can read it. Only
/// a user password keeps a file closed.
/// </remarks>
public sealed class Protection
{
    /// <summary>The password that opens the file; empty, the default, opens it without asking.</summary>
    public string UserPassword { get; init; } = string.Empty;

    /// <summary>
    /// The password that opens the file with every restriction lifted. When none is given one no one knows is made up,
    /// so the restrictions cannot be lifted at all.
    /// </summary>
    public string? OwnerPassword { get; init; }

    public EncryptionLevel Encryption { get; init; } = EncryptionLevel.AesWith256Bits;

    public bool AllowPrinting { get; init; } = true;

    /// <summary>Whether printing is faithful, rather than at the low resolution a reader falls back to.</summary>
    public bool AllowHighQualityPrinting { get; init; } = true;

    public bool AllowModifying { get; init; } = true;

    /// <summary>Whether text and images may be copied out.</summary>
    public bool AllowCopying { get; init; } = true;

    /// <summary>Whether comments may be added and form fields filled in.</summary>
    public bool AllowAnnotating { get; init; } = true;

    /// <summary>Whether form fields may be filled in even where annotating is not allowed.</summary>
    public bool AllowFillingForms { get; init; } = true;

    /// <summary>Whether assistive technology may read the content out, even where copying is not allowed.</summary>
    public bool AllowAccessibility { get; init; } = true;

    /// <summary>Whether pages may be inserted, rotated or removed, and bookmarks and thumbnails made.</summary>
    public bool AllowAssembling { get; init; } = true;

    /// <summary>Whether the XMP metadata is encrypted too; off lets search engines read it without the password.</summary>
    public bool EncryptMetadata { get; init; } = true;

    /// <summary>The permissions as the <c>/P</c> entry holds them (ISO 32000-1, table 22).</summary>
    internal int Permissions
    {
        get
        {
            // Bits 7 and 8, and 13 to 32, are reserved and must be set; bits 1 and 2 must be clear.
            int value = unchecked((int)0xFFFFF0C0);

            if (AllowPrinting)
                value |= 1 << 2;

            if (AllowModifying)
                value |= 1 << 3;

            if (AllowCopying)
                value |= 1 << 4;

            if (AllowAnnotating)
                value |= 1 << 5;

            if (AllowFillingForms)
                value |= 1 << 8;

            if (AllowAccessibility)
                value |= 1 << 9;

            if (AllowAssembling)
                value |= 1 << 10;

            if (AllowHighQualityPrinting)
                value |= 1 << 11;

            return value;
        }
    }
}
