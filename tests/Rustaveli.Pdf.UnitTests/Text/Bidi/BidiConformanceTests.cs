using System.Globalization;
using System.Text;
using Rustaveli.Pdf.Text.Bidi;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>
/// Every case of the two conformance tests Unicode publishes with the algorithm: BidiTest.txt, which states its cases
/// as sequences of classes for up to three paragraph directions each, and BidiCharacterTest.txt, which states them as
/// characters, bracket pairs among them. Both give the levels after rule L1 and the visual order after L2 for text
/// set as a single line.
/// </summary>
public class BidiConformanceTests(ITestOutputHelper output)
{
    private const int ReportedFailures = 10;

    [Fact]
    public void ResolvesEveryCaseOfBidiTest()
    {
        Dictionary<string, BidiClass> classNames = Enum.GetValues(typeof(BidiClass))
            .Cast<BidiClass>()
            .ToDictionary(type => type.ToString(), type => type);
        (int Bit, BidiDirection Direction)[] directions =
            [(1, BidiDirection.Auto), (2, BidiDirection.LeftToRight), (4, BidiDirection.RightToLeft)];

        int[] levels = [];
        int[] order = [];
        int cases = 0;
        List<string> failures = new List<string>();
        List<BidiRun> runs = new List<BidiRun>();
        int lineNumber = 0;

        foreach (string line in File.ReadLines(UnicodeFiles.PathOf("BidiTest.txt")))
        {
            lineNumber++;

            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                levels = Numbers(line.Substring("@Levels:".Length));
                continue;
            }

            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                order = Numbers(line.Substring("@Reorder:".Length));
                continue;
            }

            if (line.Length == 0 || line[0] is '#' or '@')
                continue;

            string[] fields = line.Split(';');
            BidiClass[] classes = fields[0]
                .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(name => classNames[name])
                .ToArray();
            int bitset = int.Parse(fields[1].Trim(), CultureInfo.InvariantCulture);

            foreach ((int bit, BidiDirection direction) in directions)
            {
                if ((bitset & bit) == 0)
                    continue;

                cases++;
                BidiParagraph paragraph = new BidiParagraph(classes, direction);
                string? failure = Compare(paragraph, classes, codepointOf: null, levels, order, runs);

                if (failure != null)
                    failures.Add($"line {lineNumber} ({direction}): {fields[0].Trim()}: {failure}");
            }
        }

        Report("BidiTest.txt", cases, failures);
    }

    [Fact]
    public void ResolvesEveryCaseOfBidiCharacterTest()
    {
        int cases = 0;
        List<string> failures = new List<string>();
        List<BidiRun> runs = new List<BidiRun>();
        int lineNumber = 0;

        foreach (string line in File.ReadLines(UnicodeFiles.PathOf("BidiCharacterTest.txt")))
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#')
                continue;

            string[] fields = line.Split(';');
            int[] codepoints = fields[0]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(UnicodeFiles.Hex)
                .ToArray();
            BidiDirection direction = fields[1] switch
            {
                "0" => BidiDirection.LeftToRight,
                "1" => BidiDirection.RightToLeft,
                _ => BidiDirection.Auto,
            };
            int paragraphLevel = int.Parse(fields[2], CultureInfo.InvariantCulture);

            // The file counts code points; the paragraph counts UTF-16 code units.
            StringBuilder text = new StringBuilder();
            List<int> codepointOf = new List<int>();
            for (int index = 0; index < codepoints.Length; index++)
            {
                string character = char.ConvertFromUtf32(codepoints[index]);
                text.Append(character);
                codepointOf.AddRange(Enumerable.Repeat(index, character.Length));
            }

            BidiClass[] classes = codepointOf.Select(index => BidiCharacter.ClassOf(codepoints[index])).ToArray();

            cases++;
            BidiParagraph paragraph = new BidiParagraph(text.ToString().AsSpan(), direction);
            string? failure = paragraph.ParagraphLevel != paragraphLevel
                ? $"paragraph level {paragraph.ParagraphLevel}, expected {paragraphLevel}"
                : Compare(paragraph, classes, codepointOf, Numbers(fields[3]), Numbers(fields[4]), runs);

            if (failure != null)
                failures.Add($"line {lineNumber}: {fields[0]} ({direction}): {failure}");
        }

        Report("BidiCharacterTest.txt", cases, failures);
    }

    // Compares one case, whose expectations count characters: code points, or classes where codepointOf is null.
    // Levels of -1 stand for the x the files write for characters rule X9 removes, which have neither a level nor a
    // place in the visual order.
    private static string? Compare(
        BidiParagraph paragraph, BidiClass[] classes, List<int>? codepointOf, int[] expectedLevels, int[] expectedOrder,
        List<BidiRun> runs)
    {
        int length = classes.Length;
        byte[] levels = new byte[length];
        paragraph.GetLineLevels(0, length, levels);

        List<int> actualLevels = new List<int>();
        for (int index = 0; index < length; index++)
        {
            if (codepointOf != null && index > 0 && codepointOf[index] == codepointOf[index - 1])
            {
                if (levels[index] != levels[index - 1])
                    return $"the halves of the surrogate pair at {index - 1} have levels {levels[index - 1]} and {levels[index]}";

                continue;
            }

            actualLevels.Add(BidiParagraph.IsRemovedByX9(classes[index]) ? -1 : levels[index]);
        }

        if (!actualLevels.SequenceEqual(expectedLevels))
            return $"levels {Format(actualLevels)}, expected {Format(expectedLevels)}";

        paragraph.GetVisualRuns(0, length, runs);

        List<int> actualOrder = new List<int>();
        int covered = 0;
        foreach (BidiRun run in runs)
        {
            covered += run.Length;
            for (int step = 0; step < run.Length; step++)
            {
                int index = run.IsRightToLeft ? run.Start + run.Length - 1 - step : run.Start + step;
                if (BidiParagraph.IsRemovedByX9(classes[index]))
                    continue;

                int character = codepointOf?[index] ?? index;

                // Each character once, at its first code unit in display order.
                if (actualOrder.Count == 0 || actualOrder[actualOrder.Count - 1] != character)
                    actualOrder.Add(character);
            }
        }

        if (covered != length)
            return $"runs covering {covered} of {length} code units";

        return actualOrder.SequenceEqual(expectedOrder)
            ? null
            : $"order {Format(actualOrder)}, expected {Format(expectedOrder)}";
    }

    private static int[] Numbers(string field) => field
        .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value == "x" ? -1 : int.Parse(value, CultureInfo.InvariantCulture))
        .ToArray();

    private static string Format(IEnumerable<int> values) =>
        "[" + string.Join(" ", values.Select(value => value < 0 ? "x" : value.ToString(CultureInfo.InvariantCulture))) + "]";

    private void Report(string file, int cases, List<string> failures)
    {
        output.WriteLine($"{file}: {cases - failures.Count} of {cases} cases pass");
        foreach (string failure in failures.Take(ReportedFailures))
            output.WriteLine(failure);

        Assert.True(cases > 0, $"{file} has no cases.");
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {cases} cases of {file} fail; the first:\n" + string.Join("\n", failures.Take(ReportedFailures)));
    }
}
