namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// The four-letter tag of an OpenType script, such as <c>latn</c>, held as the big-endian integer a font stores it
/// as. It chooses which of a font's features apply, since a font may set the same letters differently per script.
/// </summary>
/// <param name="Value">The tag's four bytes, the first in the most significant position.</param>
internal readonly record struct ScriptTag(uint Value)
{
    /// <summary><c>DFLT</c>: the features of text in no particular script, and the fallback for any other.</summary>
    public static readonly ScriptTag Default = new ScriptTag(TableTag.DefaultScript);

    public static readonly ScriptTag Latin = Parse("latn");

    public static readonly ScriptTag Greek = Parse("grek");

    public static readonly ScriptTag Cyrillic = Parse("cyrl");

    public static readonly ScriptTag Georgian = Parse("geor");

    public static readonly ScriptTag Armenian = Parse("armn");

    public static readonly ScriptTag Hebrew = Parse("hebr");

    public static readonly ScriptTag Arabic = Parse("arab");

    /// <summary>A tag from its four characters, such as <c>"latn"</c>.</summary>
    public static ScriptTag Parse(string tag) => new ScriptTag(TableTag.FromString(tag));

    public override string ToString() => TableTag.ToString(Value);
}
