namespace Rustaveli.Pdf.Shaping;

/// <summary>
/// The characters of the scripts whose shaping needs rules of their own, by block: a run holding any of them is
/// shaped by HarfBuzz, and every other run by the core, which sets such scripts correctly already.
/// </summary>
internal static class ComplexScriptCharacters
{
    public static bool Contains(int character) => character switch
    {
        >= '֐' and <= '׿' => true, // Hebrew, for its points and cantillation marks
        >= '؀' and <= 'ࣿ' => true, // Arabic, Syriac, Thaana, N'Ko, Samaritan, Mandaic, Arabic Extended
        >= 'ऀ' and <= '෿' => true, // Devanagari to Sinhala
        >= '฀' and <= '࿿' => true, // Thai, Lao, Tibetan
        >= 'က' and <= '႟' => true, // Myanmar
        >= 'ក' and <= '᢯' => true, // Khmer, Mongolian
        >= 'ᤀ' and <= '᪯' => true, // Limbu, Tai Le, New Tai Lue, Khmer Symbols, Buginese, Tai Tham
        >= 'ᬀ' and <= 'ᱏ' => true, // Balinese, Sundanese, Batak, Lepcha
        >= 'ꠀ' and <= '꠯' => true, // Syloti Nagri
        >= 'ꡀ' and <= 'ꣿ' => true, // Phags-pa, Saurashtra, Devanagari Extended
        >= '꤀' and <= '꫿' => true, // Kayah Li, Rejang, Javanese, Myanmar Extended, Cham, Tai Viet
        >= 'ꯀ' and <= '꯿' => true, // Meetei Mayek
        >= 0xFB1D and <= 0xFB4F => true, // Hebrew presentation forms
        >= 'ﭐ' and <= '﷿' => true, // Arabic presentation forms A
        >= 'ﹰ' and <= '﻿' => true, // Arabic presentation forms B
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
