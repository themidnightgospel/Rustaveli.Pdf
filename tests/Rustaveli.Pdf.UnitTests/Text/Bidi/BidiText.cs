using System.Text;
using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>
/// Bidirectional text written the way UAX #9 writes its examples: uppercase Latin letters stand for right-to-left
/// letters (Hebrew ones here), so that a test reads "car MEANS CAR." and expects "car means RAC." on screen.
/// </summary>
internal static class BidiText
{
    public const string LRE = "\u202A";
    public const string RLE = "\u202B";
    public const string PDF = "\u202C";
    public const string LRO = "\u202D";
    public const string RLO = "\u202E";
    public const string LRI = "\u2066";
    public const string RLI = "\u2067";
    public const string FSI = "\u2068";
    public const string PDI = "\u2069";

    private const char FirstHebrewLetter = '\u05D0';

    /// <summary>The text as stored: each uppercase Latin letter replaced by a Hebrew one.</summary>
    public static string Logical(string written)
    {
        StringBuilder logical = new StringBuilder(written.Length);
        foreach (char character in written)
            logical.Append(character is >= 'A' and <= 'Z' ? (char)(FirstHebrewLetter + (character - 'A')) : character);

        return logical.ToString();
    }

    public static BidiParagraph Paragraph(string written, BidiDirection direction = BidiDirection.Auto) =>
        new BidiParagraph(Logical(written).AsSpan(), direction);

    /// <summary>The text set as one line and read off the screen from left to right.</summary>
    public static string Display(string written, BidiDirection direction = BidiDirection.Auto) =>
        Display(Paragraph(written, direction), written, 0, written.Length);

    /// <summary>
    /// One line of a paragraph read off the screen from left to right: right-to-left runs reversed and their mirrored
    /// characters mirrored (L4), directional controls left out since they are not seen.
    /// </summary>
    public static string Display(BidiParagraph paragraph, string written, int start, int length)
    {
        List<BidiRun> runs = new List<BidiRun>();
        paragraph.GetVisualRuns(start, length, runs);

        StringBuilder display = new StringBuilder();
        foreach (BidiRun run in runs)
        {
            for (int step = 0; step < run.Length; step++)
            {
                char character = written[run.IsRightToLeft ? run.Start + run.Length - 1 - step : run.Start + step];
                BidiClass type = BidiCharacter.ClassOf(character);

                if (BidiParagraph.IsRemovedByX9(type) || type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI)
                    continue;

                display.Append(run.IsRightToLeft ? (char)BidiCharacter.Mirror(character) : character);
            }
        }

        return display.ToString();
    }

    /// <summary>The levels of the text set as one line, after L1, one digit per character.</summary>
    public static string Levels(string written, BidiDirection direction = BidiDirection.Auto) =>
        LineLevels(Paragraph(written, direction), 0, written.Length);

    public static string LineLevels(BidiParagraph paragraph, int start, int length)
    {
        byte[] levels = new byte[length];
        paragraph.GetLineLevels(start, length, levels);
        return string.Concat(levels.Select(level => level.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
