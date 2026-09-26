// Enumerates the public API of the QuestPDF oracle by reflection, as raw material for docs/parity/PARITY.md.
//
//     dotnet run eng/parity-surface.cs            writes artifacts/parity/questpdf-surface.md
//
// Reflection over the published assembly is the only way QuestPDF's surface enters this repository: its source is
// never read (docs/adr/0008-clean-room.md). The version comes from Directory.Packages.props, pinned with the test oracle.

#:package QuestPDF
#:property PublishAot=false

using System.Reflection;
using System.Text;

Assembly assembly = typeof(QuestPDF.Fluent.Document).Assembly;
StringBuilder output = new StringBuilder();
output.AppendLine($"# QuestPDF {assembly.GetName().Version} public surface");
output.AppendLine();

IEnumerable<IGrouping<string, Type>> byNamespace = assembly.GetExportedTypes()
    .Where(type => !type.IsNested || type.IsNestedPublic)
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .GroupBy(type => type.Namespace ?? "(global)");

foreach (IGrouping<string, Type> group in byNamespace)
{
    output.AppendLine($"## {group.Key}");
    output.AppendLine();

    foreach (Type type in group)
    {
        output.AppendLine($"### {Kind(type)} `{type.Name}`");

        if (type.IsEnum)
        {
            output.AppendLine(string.Join(", ", Enum.GetNames(type)));
            output.AppendLine();
            continue;
        }

        IEnumerable<string> members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(member => member is not MethodInfo { IsSpecialName: true })
            .Where(member => member.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Field)
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(text => text, StringComparer.Ordinal);

        foreach (string member in members)
            output.AppendLine($"- `{member}`");

        output.AppendLine();
    }
}

string target = Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "..", "artifacts", "parity");
Directory.CreateDirectory(target);
string file = Path.Combine(target, "questpdf-surface.md");
File.WriteAllText(file, output.ToString());
Console.WriteLine($"wrote {Path.GetFullPath(file)}");

static string Kind(Type type) =>
    type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.IsAbstract && type.IsSealed ? "static class" : "class";

static string Describe(MemberInfo member) => member switch
{
    MethodInfo method => $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})",
    PropertyInfo property => $"{property.Name} : {property.PropertyType.Name}",
    FieldInfo field => $"{field.Name} : {field.FieldType.Name}",
    _ => member.Name
};
