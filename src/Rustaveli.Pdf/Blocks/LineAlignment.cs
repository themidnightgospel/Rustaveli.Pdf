namespace Rustaveli.Pdf.Blocks;

/// <summary>How a paragraph's lines sit within the width it is given.</summary>
internal enum LineAlignment
{
    /// <summary>Flush against the edge text starts from: left in left-to-right text, right in right-to-left.</summary>
    Start,

    /// <summary>Flush against the edge text ends at.</summary>
    End,

    Left,

    Center,

    Right,

    /// <summary>
    /// Stretched across the width by widening the spaces between words, except for the last line of each
    /// paragraph, which sits flush against the start.
    /// </summary>
    Justified,
}
