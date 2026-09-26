namespace Rustaveli.Pdf.Text.LineBreaking;

/// <summary>
/// Where text may be broken into lines, by UAX #14, the Unicode Line Breaking Algorithm.
/// </summary>
/// <remarks>
/// The default, untailored rules of Unicode 16.0, which suit text of any language written with spaces between words
/// and give East Asian text its usual behaviour of breaking between ideographs. Scripts that need a dictionary to
/// find words — Thai, Lao, Khmer, Myanmar — get breaks only at spaces and punctuation.
/// </remarks>
internal static class LineBreaker
{
    /// <summary>
    /// The break opportunities in <paramref name="text"/>, in order, ending with the one at its end; none at all
    /// for empty text.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (LineBreak lineBreak in LineBreaker.Enumerate(text))
    /// {
    ///     // text[start..lineBreak.Position] can end a line; it must when lineBreak.IsMandatory.
    /// }
    /// </code>
    /// </example>
    public static LineBreakEnumerator Enumerate(ReadOnlySpan<char> text) => new LineBreakEnumerator(text);
}
