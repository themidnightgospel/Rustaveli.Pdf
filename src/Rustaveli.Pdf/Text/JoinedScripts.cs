namespace Rustaveli.Pdf.Text;

/// <summary>
/// The scripts written joined, letter to letter — Arabic, Syriac, N'Ko, Mongolian, Adlam and the like — between whose
/// letters no tracking falls: spacing them out would break the joins, so browsers leave them unspaced too.
/// </summary>
internal static class JoinedScripts
{
    public static bool Contains(int codepoint) => codepoint switch
    {
        >= 0x0600 and <= 0x077F => true, // Arabic, Syriac, Arabic Supplement
        >= 0x07C0 and <= 0x07FF => true, // N'Ko
        >= 0x0840 and <= 0x08FF => true, // Mandaic, Syriac Supplement, Arabic Extended
        >= 0x1800 and <= 0x18AF => true, // Mongolian
        >= 0xFB50 and <= 0xFDFF => true, // Arabic presentation forms A
        >= 0xFE70 and <= 0xFEFE => true, // Arabic presentation forms B
        >= 0x10AC0 and <= 0x10AFF => true, // Manichaean
        >= 0x10B80 and <= 0x10BAF => true, // Psalter Pahlavi
        >= 0x10D00 and <= 0x10D3F => true, // Hanifi Rohingya
        >= 0x10F30 and <= 0x10FAF => true, // Sogdian, Old Uyghur
        >= 0x1E900 and <= 0x1E95F => true, // Adlam
        _ => false,
    };
}
