namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// The four-letter tag of an OpenType language system, such as <c>"SRB "</c> for Serbian, held as the big-endian
/// integer a font stores it as. Within a script it chooses the features a language prefers.
/// </summary>
/// <param name="Value">The tag's four bytes, the first in the most significant position.</param>
internal readonly record struct LanguageTag(uint Value)
{
    /// <summary>
    /// <c>dflt</c>: no particular language. No font lists it, so a script's default language system applies.
    /// </summary>
    public static readonly LanguageTag Default = Parse("dflt");

    /// <summary>A tag from its four characters, padded with spaces where short, such as <c>"SRB "</c>.</summary>
    public static LanguageTag Parse(string tag) => new LanguageTag(TableTag.FromString(tag));

    public override string ToString() => TableTag.ToString(Value);
}
