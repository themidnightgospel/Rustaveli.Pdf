namespace Rustaveli.Pdf;

/// <summary>
/// Where a frame set inline with text sits against the line: on or below its baseline, or level with the top,
/// bottom or middle of the type around it.
/// </summary>
public enum InlinePosition
{
    /// <summary>Rests on the baseline, as a letter does, rising above it.</summary>
    OnBaseline,

    /// <summary>Hangs from the baseline, reaching down below it.</summary>
    BelowBaseline,

    /// <summary>Its top level with the top of the type on the line — the ascent of the tallest run.</summary>
    TextTop,

    /// <summary>Its bottom level with the bottom of the type on the line — the descent of the deepest run.</summary>
    TextBottom,

    /// <summary>Centred on the middle of the type on the line, halfway between its top and bottom.</summary>
    Middle,
}
