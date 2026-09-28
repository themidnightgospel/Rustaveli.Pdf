using System.Text.RegularExpressions;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>
/// Holds the documentation to working code: every C# example in the guides and the README appears, line for line,
/// in a test that compiles and runs it, so no example a reader copies can have gone stale.
/// </summary>
/// <remarks>
/// Lines are compared without their indentation, blank lines are skipped, and a <c>using</c> directive naming a
/// namespace is taken as read — a test file has its own. Anything else that differs fails.
/// </remarks>
public class GuideTests
{
    private static readonly Regex CodeBlock = new Regex("```csharp\\r?\\n(.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex UsingDirective = new Regex(@"^using [A-Za-z][\w.]*;$", RegexOptions.Compiled);

    public static TheoryData<string> Documents()
    {
        TheoryData<string> documents = new TheoryData<string> { "README.md" };

        foreach (string guide in Directory.GetFiles(Path.Combine(RepositoryPaths.Root, "docs", "guide"), "*.md").OrderBy(path => path, StringComparer.Ordinal))
            documents.Add("docs/guide/" + Path.GetFileName(guide));

        return documents;
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryExampleIsCodeATestRuns(string document)
    {
        string text = File.ReadAllText(Path.Combine(RepositoryPaths.Root, document));
        string tests = Tests();

        foreach (Match block in CodeBlock.Matches(text))
        {
            string example = Normalize(block.Groups[1].Value);
            string opening = example.Split('\n')[0];

            Assert.True(tests.Contains(example, StringComparison.Ordinal), $"{document}: the example beginning \"{opening}\" is not run by any test.");
        }
    }

    [Fact]
    public void EveryGuideIsListedInTheIndex()
    {
        string index = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "guide", "README.md"));

        foreach (string guide in Directory.GetFiles(Path.Combine(RepositoryPaths.Root, "docs", "guide"), "*.md"))
        {
            string name = Path.GetFileName(guide);

            if (name != "README.md")
                Assert.Contains("(" + name + ")", index, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LinesAreComparedWithoutIndentationBlankLinesOrUsingDirectives() =>
        Assert.Equal("Document document = Make();\nusing PdfFile file = Open();", Normalize("using Rustaveli.Pdf;\n\n    Document document = Make();\r\n\tusing PdfFile file = Open();\n"));

    /// <summary>The guide tests and the README's quick start, as one text.</summary>
    private static string Tests()
    {
        IEnumerable<string> files = Directory.GetFiles(Path.Combine(RepositoryPaths.Project, "Guide"), "*.cs")
            .Append(Path.Combine(RepositoryPaths.Root, "tests", "Rustaveli.Pdf.IntegrationTests", "QuickStartTests.cs"));

        return string.Join("\n", files.Select(file => Normalize(File.ReadAllText(file))));
    }

    private static string Normalize(string code) =>
        string.Join("\n", code.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !UsingDirective.IsMatch(line)));
}
