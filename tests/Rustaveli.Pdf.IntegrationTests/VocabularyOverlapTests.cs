using System.Reflection;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Keeps the public vocabulary this library's own: names it shares with QuestPDF must be ordinary English of
/// layout and typography, never that library's coinages.
/// </summary>
/// <remarks>
/// A name added here is a decision: it has to be the plain word a user of any page-layout tool would reach for.
/// See <c>docs/GLOSSARY.md</c> and ADR 0002.
/// </remarks>
public class VocabularyOverlapTests
{
    /// <summary>Plain words of layout and typography, and the names .NET itself gives such members.</summary>
    private static readonly HashSet<string> SharedMethodsAllowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // Object, record and .NET conventions.
        "<Clone>$", "Dispose", "Equals", "Format", "FromFile", "FromStream", "GetHashCode", "ToString",

        // Layout.
        "Cell", "Columns", "Compose", "Height", "Image", "Landscape", "Layer", "MaxHeight", "MaxWidth", "MinHeight",
        "MinWidth", "Placeholder", "Portrait", "Scale", "Section", "Stack", "Table", "Width",

        // Typography.
        "Bold", "Italic", "Line", "Overline", "Style", "Subscript", "Superscript", "Text", "Underline", "Weight",
        "WordSpacing",
    };

    private static readonly HashSet<string> SharedTypesAllowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "Document",
    };

    private static readonly Assembly[] Ours = [typeof(Document).Assembly, typeof(ImageExport).Assembly];

    private static readonly Assembly Theirs = typeof(QuestPDF.Fluent.Document).Assembly;

    [Fact]
    public void SharesOnlyPlainWordsForMethods()
    {
        HashSet<string> theirs = PublicMethodNames(Theirs);

        List<string> coined = Ours.SelectMany(PublicMethodNames)
            .Where(theirs.Contains)
            .Where(name => !SharedMethodsAllowed.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(coined.Count == 0, "Method names shared with QuestPDF that are not plain words: " + string.Join(", ", coined));
    }

    [Fact]
    public void SharesOnlyPlainWordsForTypes()
    {
        HashSet<string> theirs = PublicTypeNames(Theirs);

        List<string> coined = Ours.SelectMany(PublicTypeNames)
            .Where(theirs.Contains)
            .Where(name => !SharedTypesAllowed.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(coined.Count == 0, "Type names shared with QuestPDF that are not plain words: " + string.Join(", ", coined));
    }

    private static HashSet<string> PublicMethodNames(Assembly assembly) =>
        new HashSet<string>(
            assembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name),
            StringComparer.Ordinal);

    private static HashSet<string> PublicTypeNames(Assembly assembly) =>
        new HashSet<string>(assembly.GetExportedTypes().Select(type => type.Name), StringComparer.Ordinal);
}
