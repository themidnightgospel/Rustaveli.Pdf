namespace Rustaveli.Pdf.Shaping;

/// <summary>
/// The characters of the scripts whose shaping needs rules of their own, by block: a run holding any of them is
/// shaped by HarfBuzz, and every other run by the core, which sets such scripts correctly already.
/// </summary>
internal static class ComplexScriptCharacters
{
    public static bool Contains(int character) => character switch
    {
        >= 0x0590 and <= 0x05FF => true, // Hebrew, for its points and cantillation marks
        >= 0x0600 and <= 0x08FF => true, // Arabic, Syriac, Thaana, N'Ko, Samaritan, Mandaic, Arabic Extended
        >= 0x0900 and <= 0x0DFF => true, // Devanagari to Sinhala
        >= 0x0E00 and <= 0x0FFF => true, // Thai, Lao, Tibetan
        >= 0x1000 and <= 0x109F => true, // Myanmar
        >= 0x1780 and <= 0x18AF => true, // Khmer, Mongolian
        >= 0x1900 and <= 0x1AAF => true, // Limbu, Tai Le, New Tai Lue, Khmer Symbols, Buginese, Tai Tham
        >= 0x1B00 and <= 0x1C4F => true, // Balinese, Sundanese, Batak, Lepcha
        >= 0xA800 and <= 0xA82F => true, // Syloti Nagri
        >= 0xA840 and <= 0xA8FF => true, // Phags-pa, Saurashtra, Devanagari Extended
        >= 0xA900 and <= 0xAAFF => true, // Kayah Li, Rejang, Javanese, Myanmar Extended, Cham, Tai Viet
        >= 0xABC0 and <= 0xABFF => true, // Meetei Mayek
        >= 0xFB1D and <= 0xFB4F => true, // Hebrew presentation forms
        >= 0xFB50 and <= 0xFDFF => true, // Arabic presentation forms A
        >= 0xFE70 and <= 0xFEFF => true, // Arabic presentation forms B
        >= 0x10A00 and <= 0x10A5F => true, // Kharoshthi
        >= 0x10AC0 and <= 0x10AFF => true, // Manichaean
        >= 0x10B80 and <= 0x10BAF => true, // Psalter Pahlavi
        >= 0x10D00 and <= 0x10D3F => true, // Hanifi Rohingya
        >= 0x10F30 and <= 0x10FDF => true, // Sogdian, Old Uyghur, Chorasmian
        >= 0x11000 and <= 0x11AFF => true, // Brahmi, Kaithi, Chakma, Sharada, Grantha, Newa, Tirhuta, Modi, Takri, Ahom and more
        >= 0x11C00 and <= 0x11DAF => true, // Bhaiksuki, Marchen, Masaram Gondi, Gunjala Gondi
        >= 0x11EE0 and <= 0x11F5F => true, // Makasar, Kawi
        >= 0x1E900 and <= 0x1E95F => true, // Adlam
        _ => false,
    };
}
