namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// A stretch of a line whose characters share one resolved level, and so one direction.
/// </summary>
/// <param name="Start">Where the run starts in the paragraph, in UTF-16 code units.</param>
/// <param name="Length">The run's length in UTF-16 code units.</param>
/// <param name="Level">The run's resolved level: even runs left to right, odd right to left.</param>
internal readonly record struct BidiRun(int Start, int Length, int Level)
{
    /// <summary>
    /// Whether the run's characters are displayed right to left: set from its last character to its first, with
    /// mirrored glyphs for the characters that have one (rule L4).
    /// </summary>
    public bool IsRightToLeft => (Level & 1) != 0;
}
