namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The platform IDs that <c>cmap</c> subtables and <c>name</c> records are tagged with, and the encoding IDs each
/// platform numbers on its own.
/// </summary>
internal static class PlatformEncoding
{
    public const ushort UnicodePlatform = 0;
    public const ushort MacintoshPlatform = 1;
    public const ushort WindowsPlatform = 3;

    // Unicode platform encodings: the version of the standard a map follows, and how much of it the map covers.
    public const ushort Unicode10 = 0;
    public const ushort Unicode11 = 1;
    public const ushort Iso10646 = 2;
    public const ushort UnicodeBmp = 3;
    public const ushort UnicodeFull = 4;
    public const ushort UnicodeVariationSequences = 5;

    /// <summary>The full repertoire, mapped by a last-resort font's format 13 subtable.</summary>
    public const ushort UnicodeLastResort = 6;

    // Macintosh platform encodings.
    public const ushort MacintoshRoman = 0;

    // Windows platform encodings.
    public const ushort WindowsSymbol = 0;
    public const ushort WindowsUnicodeBmp = 1;
    public const ushort WindowsUnicodeFull = 10;
}
