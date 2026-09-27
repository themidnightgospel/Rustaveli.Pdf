namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The four-byte tags that name OpenType tables and layout features, as the big-endian integers they are stored as.
/// </summary>
/// <remarks>
/// Kept as integers rather than strings so a table lookup compares one number instead of allocating and comparing
/// text.
/// </remarks>
internal static class TableTag
{
    public const uint Cff = 0x43464620; // 'CFF '
    public const uint Cff2 = 0x43464632; // 'CFF2'
    public const uint Cmap = 0x636D6170; // 'cmap'
    public const uint Cvt = 0x63767420; // 'cvt '
    public const uint Fpgm = 0x6670676D; // 'fpgm'
    public const uint Glyf = 0x676C7966; // 'glyf'
    public const uint Gpos = 0x47504F53; // 'GPOS'
    public const uint Head = 0x68656164; // 'head'
    public const uint Hhea = 0x68686561; // 'hhea'
    public const uint Hmtx = 0x686D7478; // 'hmtx'
    public const uint Kern = 0x6B65726E; // 'kern'
    public const uint Loca = 0x6C6F6361; // 'loca'
    public const uint Maxp = 0x6D617870; // 'maxp'
    public const uint Name = 0x6E616D65; // 'name'
    public const uint Os2 = 0x4F532F32; // 'OS/2'
    public const uint Post = 0x706F7374; // 'post'
    public const uint Prep = 0x70726570; // 'prep'

    /// <summary>The default script in a GPOS script list.</summary>
    public const uint DefaultScript = 0x44464C54; // 'DFLT'

    public static uint FromString(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Length != 4)
            throw new ArgumentException("A tag is exactly four characters.", nameof(tag));

        return ((uint)tag[0] << 24) | ((uint)tag[1] << 16) | ((uint)tag[2] << 8) | tag[3];
    }

    public static string ToString(uint tag)
    {
        char[] characters =
            [(char)(tag >> 24), (char)((tag >> 16) & 0xFF), (char)((tag >> 8) & 0xFF), (char)(tag & 0xFF)];

        return new string(characters);
    }
}
