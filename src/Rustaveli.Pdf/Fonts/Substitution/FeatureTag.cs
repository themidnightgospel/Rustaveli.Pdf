using System.Globalization;

namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// The four-letter tag of an OpenType layout feature, such as <c>liga</c> for standard ligatures, held as the
/// big-endian integer a font stores it as.
/// </summary>
/// <param name="Value">The tag's four bytes, the first in the most significant position.</param>
internal readonly record struct FeatureTag(uint Value)
{
    /// <summary><c>rvrn</c>: glyphs a variable font swaps in at some axis settings.</summary>
    public static readonly FeatureTag RequiredVariationAlternates = Parse("rvrn");

    /// <summary><c>ccmp</c>: composes and decomposes, such as an i before an accent into a dotless i.</summary>
    public static readonly FeatureTag GlyphComposition = Parse("ccmp");

    /// <summary><c>locl</c>: the forms a language prefers, such as Serbian Cyrillic italics.</summary>
    public static readonly FeatureTag LocalizedForms = Parse("locl");

    /// <summary><c>rlig</c>: ligatures a script cannot be written without.</summary>
    public static readonly FeatureTag RequiredLigatures = Parse("rlig");

    /// <summary><c>calt</c>: alternates chosen by the surrounding glyphs.</summary>
    public static readonly FeatureTag ContextualAlternates = Parse("calt");

    /// <summary><c>clig</c>: ligatures formed only in some surroundings.</summary>
    public static readonly FeatureTag ContextualLigatures = Parse("clig");

    /// <summary><c>liga</c>: the ligatures set by default, such as fi and ffl.</summary>
    public static readonly FeatureTag StandardLigatures = Parse("liga");

    /// <summary><c>dlig</c>: ligatures for special effect, off by default.</summary>
    public static readonly FeatureTag DiscretionaryLigatures = Parse("dlig");

    /// <summary><c>smcp</c>: lowercase letters as small capitals.</summary>
    public static readonly FeatureTag SmallCapitals = Parse("smcp");

    /// <summary><c>c2sc</c>: capitals as small capitals.</summary>
    public static readonly FeatureTag CapitalsToSmallCapitals = Parse("c2sc");

    /// <summary><c>onum</c>: figures with ascenders and descenders, to sit in running text.</summary>
    public static readonly FeatureTag OldstyleFigures = Parse("onum");

    /// <summary><c>lnum</c>: figures the height of capitals.</summary>
    public static readonly FeatureTag LiningFigures = Parse("lnum");

    /// <summary><c>pnum</c>: figures each as wide as its shape.</summary>
    public static readonly FeatureTag ProportionalFigures = Parse("pnum");

    /// <summary><c>tnum</c>: figures all of one width, so columns of numbers align.</summary>
    public static readonly FeatureTag TabularFigures = Parse("tnum");

    /// <summary><c>frac</c>: a numerator, slash and denominator set as one fraction.</summary>
    public static readonly FeatureTag Fractions = Parse("frac");

    /// <summary><c>sups</c>: superior (superscript) forms.</summary>
    public static readonly FeatureTag Superscript = Parse("sups");

    /// <summary><c>subs</c>: inferior (subscript) forms.</summary>
    public static readonly FeatureTag Subscript = Parse("subs");

    /// <summary><c>salt</c>: stylistic alternates, of which a setting's value chooses one.</summary>
    public static readonly FeatureTag StylisticAlternates = Parse("salt");

    /// <summary><c>init</c>: the form of a joining letter at the start of a word.</summary>
    public static readonly FeatureTag InitialForms = Parse("init");

    /// <summary><c>medi</c>: the form of a joining letter inside a word.</summary>
    public static readonly FeatureTag MedialForms = Parse("medi");

    /// <summary><c>fina</c>: the form of a joining letter at the end of a word.</summary>
    public static readonly FeatureTag TerminalForms = Parse("fina");

    /// <summary><c>isol</c>: the form of a joining letter standing alone.</summary>
    public static readonly FeatureTag IsolatedForms = Parse("isol");

    /// <summary>A tag from its four characters, such as <c>"liga"</c>.</summary>
    public static FeatureTag Parse(string tag) => new FeatureTag(TableTag.FromString(tag));

    /// <summary>Stylistic set <paramref name="number"/>, <c>ss01</c> to <c>ss20</c>.</summary>
    public static FeatureTag StylisticSet(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, 20);

        return Parse("ss" + number.ToString("00", CultureInfo.InvariantCulture));
    }

    public override string ToString() => TableTag.ToString(Value);
}
