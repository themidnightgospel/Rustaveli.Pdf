using System.Reflection;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Holds the public API to its documented vocabulary: every public type is named in <c>docs/GLOSSARY.md</c>, and
/// every one lives in the <c>Rustaveli.Pdf</c> namespace so that one using directive is enough.
/// </summary>
public class GlossaryTests
{
    private static readonly Assembly[] Packages = [typeof(Document).Assembly];

    private static readonly string Glossary =
        File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "GLOSSARY.md"));

    public static TheoryData<string> PublicTypes()
    {
        TheoryData<string> types = new TheoryData<string>();
        foreach (Type type in Packages.SelectMany(assembly => assembly.GetExportedTypes()))
            types.Add(type.FullName!);

        return types;
    }

    [Theory]
    [MemberData(nameof(PublicTypes))]
    public void NamesEveryPublicType(string fullName)
    {
        string name = NameOf(fullName);

        // Named in its own right (`Section`), by a member (`Section.Trim`), a call or a generic (`Snippet<T>`).
        bool named = new[] { "`", ".", "(", "<" }.Any(next => Glossary.Contains("`" + name + next, StringComparison.Ordinal));

        Assert.True(named, $"docs/GLOSSARY.md does not name the public type {name}.");
    }

    [Theory]
    [MemberData(nameof(PublicTypes))]
    public void KeepsEveryPublicTypeInTheRootNamespace(string fullName)
    {
        Assert.Equal("Rustaveli.Pdf." + NameOf(fullName), fullName.Split('`')[0]);
    }

    private static string NameOf(string fullName) => fullName.Split('`')[0].Split('.', '+').Last();
}
