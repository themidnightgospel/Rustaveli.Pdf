using System.Text;
using Rustaveli.Pdf.Text.LineBreaking;

namespace Rustaveli.Pdf.UnitTests.LineBreaking;

/// <summary>
/// The line breaker against LineBreakTest.txt, the test file Unicode publishes with the standard: every case in it,
/// none skipped.
/// </summary>
public class LineBreakConformanceTests
{
    private const string TestFile = "LineBreakTest.txt";

    [Fact]
    public void BreaksEveryCaseOfTheUnicodeTestFileWhereTheStandardDoes()
    {
        List<string> failures = [];
        int cases = 0;
        int lineNumber = 0;

        foreach (string line in File.ReadLines(UnicodeData.PathOf(TestFile)))
        {
            lineNumber++;
            int comment = line.IndexOf('#');
            string data = (comment < 0 ? line : line.Substring(0, comment)).Trim();

            if (data.Length == 0)
                continue;

            cases++;
            (string text, List<int> expected) = Parse(data);
            List<int> actual = Positions(text);

            if (!actual.SequenceEqual(expected) && failures.Count < 20)
            {
                failures.Add(
                    $"line {lineNumber}: {data}{Environment.NewLine}" +
                    $"    expected breaks at [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]" +
                    $"{Environment.NewLine}    {line.Substring(comment + 1).Trim()}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

        // Every line of the Unicode 16.0.0 file was run: a parsing slip that dropped cases would show here.
        Assert.Equal(16672, cases);
    }

    /// <summary>
    /// A test case's text, and the UTF-16 positions it expects breaks at: a ÷ before a code point is a break
    /// before it, and the ÷ that ends every case is the break at the end of the text.
    /// </summary>
    private static (string Text, List<int> Breaks) Parse(string data)
    {
        StringBuilder text = new StringBuilder();
        List<int> breaks = [];

        foreach (string token in data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "÷")
                breaks.Add(text.Length);
            else if (token != "×")
                text.Append(char.ConvertFromUtf32(UnicodeData.Hex(token)));
        }

        return (text.ToString(), breaks);
    }

    private static List<int> Positions(string text)
    {
        List<int> positions = [];

        foreach (LineBreak lineBreak in LineBreaker.Enumerate(text.AsSpan()))
            positions.Add(lineBreak.Position);

        return positions;
    }
}
