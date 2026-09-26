using System.Reflection;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Reports which public API names this library shares with QuestPDF.
/// </summary>
/// <remarks>
/// Diagnostic only — it asserts nothing. The point is to distinguish names that are genuinely QuestPDF's
/// coinages from names that are simply the ordinary English of layout, so that any renaming effort is aimed at
/// the former rather than churning the latter.
/// </remarks>
public class VocabularyOverlapTests(ITestOutputHelper output)
{
    private static HashSet<string> PublicMethodNames(Assembly assembly) =>
        new HashSet<string>(
            assembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name),
            StringComparer.Ordinal);

    private static HashSet<string> PublicTypeNames(Assembly assembly) =>
        new HashSet<string>(assembly.GetExportedTypes().Select(type => type.Name), StringComparer.Ordinal);

    [Fact]
    public void ReportSharedVocabulary()
    {
        Assembly ours = typeof(Document).Assembly;
        Assembly theirs = typeof(QuestPDF.Fluent.Document).Assembly;

        HashSet<string> ourMethods = PublicMethodNames(ours);
        HashSet<string> theirMethods = PublicMethodNames(theirs);
        List<string> sharedMethods = ourMethods.Intersect(theirMethods, StringComparer.Ordinal).OrderBy(name => name).ToList();

        HashSet<string> ourTypes = PublicTypeNames(ours);
        HashSet<string> theirTypes = PublicTypeNames(theirs);
        List<string> sharedTypes = ourTypes.Intersect(theirTypes, StringComparer.Ordinal).OrderBy(name => name).ToList();

        output.WriteLine($"our public methods   : {ourMethods.Count}");
        output.WriteLine($"their public methods : {theirMethods.Count}");
        output.WriteLine($"shared method names  : {sharedMethods.Count} ({100.0 * sharedMethods.Count / ourMethods.Count:F0}% of ours)");
        output.WriteLine(string.Empty);
        output.WriteLine("SHARED METHOD NAMES:");
        output.WriteLine(string.Join(", ", sharedMethods));
        output.WriteLine(string.Empty);
        output.WriteLine("SHARED TYPE NAMES:");
        output.WriteLine(string.Join(", ", sharedTypes));
        output.WriteLine(string.Empty);
        output.WriteLine("OURS ONLY (methods):");
        output.WriteLine(string.Join(", ", ourMethods.Except(theirMethods, StringComparer.Ordinal).OrderBy(name => name)));
    }
}
