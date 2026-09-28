using System.Globalization;
using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>
/// Paragraphs written as sequences of classes, "R EN ES EN", as BidiTest.txt writes its cases: a rule can be exercised
/// by the classes it is about without hunting for characters that have them.
/// </summary>
internal static class BidiClasses
{
    public static BidiClass[] Parse(string names) => names
        .Split([' '], StringSplitOptions.RemoveEmptyEntries)
        .Select(name => (BidiClass)Enum.Parse(typeof(BidiClass), name))
        .ToArray();

    public static BidiParagraph Paragraph(string names, BidiDirection direction = BidiDirection.Auto) =>
        new BidiParagraph(Parse(names), direction);

    /// <summary>
    /// The levels of the classes set as one line, after L1, separated by spaces; x for a class rule X9 removes.
    /// </summary>
    public static string Levels(string names, BidiDirection direction = BidiDirection.Auto)
    {
        BidiClass[] classes = Parse(names);
        byte[] levels = new byte[classes.Length];
        new BidiParagraph(classes, direction).GetLineLevels(0, classes.Length, levels);

        return string.Join(
            " ",
            classes.Select((type, index) =>
                BidiParagraph.IsRemovedByX9(type) ? "x" : levels[index].ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The levels the paragraph resolved, before any line is reordered, separated by spaces.</summary>
    public static string ResolvedLevels(string names, BidiDirection direction = BidiDirection.Auto)
    {
        BidiParagraph paragraph = Paragraph(names, direction);
        return string.Join(
            " ",
            Enumerable.Range(0, paragraph.Length).Select(index => paragraph.GetLevel(index).ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// The positions of the classes in the order they are displayed from left to right, those X9 removes left out.
    /// </summary>
    public static string Order(string names, BidiDirection direction = BidiDirection.Auto)
    {
        BidiClass[] classes = Parse(names);
        List<BidiRun> runs = new List<BidiRun>();
        new BidiParagraph(classes, direction).GetVisualRuns(0, classes.Length, runs);

        IEnumerable<int> order = runs
            .SelectMany(run => run.IsRightToLeft
                ? Enumerable.Range(run.Start, run.Length).Reverse()
                : Enumerable.Range(run.Start, run.Length))
            .Where(index => !BidiParagraph.IsRemovedByX9(classes[index]));

        return string.Join(" ", order);
    }

    /// <summary>A direction by name, as a public test method has to take it.</summary>
    public static BidiDirection Direction(string name) => (BidiDirection)Enum.Parse(typeof(BidiDirection), name);

    /// <summary>The names repeated, for paragraphs that climb to the depth limit.</summary>
    public static string Repeat(string name, int count) => string.Join(" ", Enumerable.Repeat(name, count));
}
