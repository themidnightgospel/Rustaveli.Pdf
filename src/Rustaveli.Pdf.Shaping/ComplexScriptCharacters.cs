namespace Rustaveli.Pdf.Shaping;

/// <summary>
/// The characters of the scripts whose shaping needs rules of their own, by block: a run holding any of them is
/// shaped by HarfBuzz, and every other run by the core, which sets such scripts correctly already.
/// </summary>
internal static class ComplexScriptCharacters
{
    public static bool Contains(char character) => character switch
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
        >= 'יִ' and <= 'ﭏ' => true, // Hebrew presentation forms
        >= 'ﭐ' and <= '﷿' => true, // Arabic presentation forms A
        >= 'ﹰ' and <= '﻿' => true, // Arabic presentation forms B
        _ => false,
    };
}
