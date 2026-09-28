namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// The Bidi_Class property: the directional type the bidirectional algorithm starts every character from.
/// </summary>
/// <remarks>
/// Named by the short aliases UAX #9 writes its rules in, so that the code can be read against the specification rule
/// by rule. The values are the ones <see cref="BidiCharacterTables"/> stores, so their order is fixed.
/// </remarks>
internal enum BidiClass : byte
{
    /// <summary>Left-to-right: Latin, Greek, Cyrillic, Han and most other scripts, and LRM.</summary>
    L,

    /// <summary>Right-to-left: Hebrew and other right-to-left scripts, and RLM.</summary>
    R,

    /// <summary>Right-to-left Arabic: Arabic, Syriac and Thaana letters, and ALM.</summary>
    AL,

    /// <summary>European number: European and Eastern Arabic-Indic digits.</summary>
    EN,

    /// <summary>European separator: plus and minus signs.</summary>
    ES,

    /// <summary>European terminator: degree, percent and currency signs.</summary>
    ET,

    /// <summary>Arabic number: Arabic-Indic digits and the Arabic decimal and thousands separators.</summary>
    AN,

    /// <summary>Common separator: colon, comma, full stop and no-break space.</summary>
    CS,

    /// <summary>Nonspacing mark: combining marks, which take the type of the character they follow.</summary>
    NSM,

    /// <summary>Boundary neutral: controls and default ignorables, which the algorithm passes over.</summary>
    BN,

    /// <summary>Paragraph separator.</summary>
    B,

    /// <summary>Segment separator: tab.</summary>
    S,

    /// <summary>White space.</summary>
    WS,

    /// <summary>Other neutral: punctuation and symbols without a direction of their own.</summary>
    ON,

    /// <summary>Left-to-right embedding, U+202A.</summary>
    LRE,

    /// <summary>Left-to-right override, U+202D.</summary>
    LRO,

    /// <summary>Right-to-left embedding, U+202B.</summary>
    RLE,

    /// <summary>Right-to-left override, U+202E.</summary>
    RLO,

    /// <summary>Pop directional formatting, U+202C.</summary>
    PDF,

    /// <summary>Left-to-right isolate, U+2066.</summary>
    LRI,

    /// <summary>Right-to-left isolate, U+2067.</summary>
    RLI,

    /// <summary>First strong isolate, U+2068.</summary>
    FSI,

    /// <summary>Pop directional isolate, U+2069.</summary>
    PDI,
}
