namespace Rustaveli.Pdf.Writing;

/// <summary>
/// PDFDocEncoding (ISO 32000-1, Annex D.2), the single-byte encoding PDF text strings use unless they carry a
/// UTF-16BE byte order mark.
/// </summary>
/// <remarks>
/// It is Latin-1 with the C1 range replaced by typographic punctuation, which is what document titles and outline
/// entries in Western languages mostly contain — so most of them fit one byte per character instead of two.
/// </remarks>
internal static class PdfDocEncoding
{
    /// <summary>Returns the byte encoding <paramref name="character"/>, or -1 when PDFDocEncoding has none.</summary>
    public static int Encode(char character)
    {
        if (character is '\t' or '\n' or '\r' or (>= ' ' and <= '~'))
            return character;

        // Latin-1's upper half maps to itself, except the soft hyphen, which PDFDocEncoding leaves undefined.
        if (character is >= '¡' and <= 'ÿ' and not '­')
            return character;

        return character switch
        {
            '˘' => 0x18, // breve
            'ˇ' => 0x19, // caron
            'ˆ' => 0x1A, // modifier circumflex
            '˙' => 0x1B, // dot above
            '˝' => 0x1C, // double acute
            '˛' => 0x1D, // ogonek
            '˚' => 0x1E, // ring above
            '˜' => 0x1F, // small tilde
            '•' => 0x80, // bullet
            '†' => 0x81, // dagger
            '‡' => 0x82, // double dagger
            '…' => 0x83, // horizontal ellipsis
            '—' => 0x84, // em dash
            '–' => 0x85, // en dash
            'ƒ' => 0x86, // florin
            '⁄' => 0x87, // fraction slash
            '‹' => 0x88, // single left-pointing angle quotation mark
            '›' => 0x89, // single right-pointing angle quotation mark
            '−' => 0x8A, // minus sign
            '‰' => 0x8B, // per mille sign
            '„' => 0x8C, // double low-9 quotation mark
            '“' => 0x8D, // left double quotation mark
            '”' => 0x8E, // right double quotation mark
            '‘' => 0x8F, // left single quotation mark
            '’' => 0x90, // right single quotation mark
            '‚' => 0x91, // single low-9 quotation mark
            '™' => 0x92, // trade mark sign
            'ﬁ' => 0x93, // fi ligature
            'ﬂ' => 0x94, // fl ligature
            'Ł' => 0x95, // L with stroke
            'Œ' => 0x96, // ligature OE
            'Š' => 0x97, // S with caron
            'Ÿ' => 0x98, // Y with diaeresis
            'Ž' => 0x99, // Z with caron
            'ı' => 0x9A, // dotless i
            'ł' => 0x9B, // l with stroke
            'œ' => 0x9C, // ligature oe
            'š' => 0x9D, // s with caron
            'ž' => 0x9E, // z with caron
            '€' => 0xA0, // euro sign
            _ => -1,
        };
    }
}
