namespace Rustaveli.Pdf;

/// <summary>
/// How a list marks each of its items.
/// </summary>
public enum ListNumbering
{
    /// <summary>A bullet character, the same for every item.</summary>
    Bullet,

    /// <summary>An arabic numeral counting from the list's start.</summary>
    Arabic,

    /// <summary>A lower-case letter: a, b, c … continuing aa, ab beyond the alphabet.</summary>
    LowerAlpha,

    /// <summary>An upper-case letter.</summary>
    UpperAlpha,

    /// <summary>A lower-case roman numeral.</summary>
    LowerRoman,

    /// <summary>An upper-case roman numeral.</summary>
    UpperRoman
}
