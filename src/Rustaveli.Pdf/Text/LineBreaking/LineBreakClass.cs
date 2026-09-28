namespace Rustaveli.Pdf.Text.LineBreaking;

/// <summary>
/// The Line_Break property values of UAX #14, as rule LB1 resolves them when nothing is known about the text's
/// language.
/// </summary>
/// <remarks>
/// <para>
/// Members carry Unicode's own short names so that each rule of the line breaker reads the way the standard writes
/// it. The classes LB1 resolves away — AI, SG and XX to AL, SA to CM or AL, CJ to NS — never appear: the table
/// already holds what they resolve to.
/// </para>
/// <para>
/// QU is split three ways. Rules LB15a, LB15b and LB19 treat a quotation mark differently when its general category
/// is initial or final punctuation, and carrying that in the class saves a second lookup for every character.
/// </para>
/// <para>
/// The numbering is load-bearing. eng/unicode-line-break.cs writes the table with these values, and the rules rely
/// on <see cref="Sot"/> through <see cref="ZW"/> coming first: those are exactly the classes a combining mark cannot
/// attach to.
/// </para>
/// </remarks>
internal enum LineBreakClass : byte
{
    /// <summary>
    /// The start of the text. Not a property value: it stands in for the missing neighbour of the first character,
    /// which several rules name as <c>sot</c>.
    /// </summary>
    Sot,

    /// <summary>The end of the text, the <c>eot</c> of the rules. Not a property value either.</summary>
    Eot,

    /// <summary>Mandatory break: form feed, vertical tab, line and paragraph separators.</summary>
    BK,

    /// <summary>Carriage return.</summary>
    CR,

    /// <summary>Line feed.</summary>
    LF,

    /// <summary>Next line, U+0085.</summary>
    NL,

    /// <summary>Space.</summary>
    SP,

    /// <summary>Zero width space.</summary>
    ZW,

    /// <summary>Combining mark, and most control and format characters.</summary>
    CM,

    /// <summary>Zero width joiner.</summary>
    ZWJ,

    /// <summary>Word joiner.</summary>
    WJ,

    /// <summary>Non-breaking ("glue"): no-break space and its relatives.</summary>
    GL,

    /// <summary>Contingent break opportunity: an embedded object.</summary>
    CB,

    /// <summary>Aksara: a consonant of a Brahmic script that forms orthographic syllables.</summary>
    AK,

    /// <summary>Ordinary alphabetic and symbol characters.</summary>
    AL,

    /// <summary>Aksara pre-base.</summary>
    AP,

    /// <summary>Aksara start: an independent vowel.</summary>
    AS,

    /// <summary>Break opportunity before and after: the em dash.</summary>
    B2,

    /// <summary>Break after: hyphens that allow a break after them, and most spaces of fixed width.</summary>
    BA,

    /// <summary>Break before: acute accents and similar.</summary>
    BB,

    /// <summary>Closing punctuation.</summary>
    CL,

    /// <summary>Closing parenthesis and square bracket.</summary>
    CP,

    /// <summary>Emoji base: an emoji that takes a skin tone modifier.</summary>
    EB,

    /// <summary>Emoji modifier: a skin tone.</summary>
    EM,

    /// <summary>Exclamation and interrogation marks.</summary>
    EX,

    /// <summary>Hangul LV syllable.</summary>
    H2,

    /// <summary>Hangul LVT syllable.</summary>
    H3,

    /// <summary>Hebrew letter.</summary>
    HL,

    /// <summary>Hyphen-minus.</summary>
    HY,

    /// <summary>Ideographic.</summary>
    ID,

    /// <summary>Inseparable: leaders and ellipses.</summary>
    IN,

    /// <summary>Infix numeric separator: comma, full stop, colon, semicolon.</summary>
    IS,

    /// <summary>Hangul leading jamo.</summary>
    JL,

    /// <summary>Hangul trailing jamo.</summary>
    JT,

    /// <summary>Hangul vowel jamo.</summary>
    JV,

    /// <summary>Nonstarter: small kana, iteration marks and the like.</summary>
    NS,

    /// <summary>Numeric.</summary>
    NU,

    /// <summary>Opening punctuation.</summary>
    OP,

    /// <summary>Postfix numeric: percent, degree and similar signs.</summary>
    PO,

    /// <summary>Prefix numeric: currency signs and similar.</summary>
    PR,

    /// <summary>Quotation mark that is neither initial nor final punctuation, such as <c>"</c>.</summary>
    QU,

    /// <summary>Quotation mark with general category Pi, initial punctuation, such as <c>“</c>.</summary>
    QUPi,

    /// <summary>Quotation mark with general category Pf, final punctuation, such as <c>”</c>.</summary>
    QUPf,

    /// <summary>Regional indicator: half of a flag.</summary>
    RI,

    /// <summary>Symbols allowing a break after: the solidus.</summary>
    SY,

    /// <summary>Virama final.</summary>
    VF,

    /// <summary>Virama.</summary>
    VI
}
