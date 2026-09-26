// Promotes received visual snapshots to approved, after a deliberate change to what pages look like.
//
//     dotnet run eng/approve-snapshots.cs              approve every received snapshot
//     dotnet run eng/approve-snapshots.cs -- gallery   approve only names starting with "gallery"
//
// Look at artifacts/snapshots/*.received.png (and *.diff.png) before approving: approval is the review.

#:property PublishAot=false

string root = Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", ".."));
string received = Path.Combine(root, "artifacts", "snapshots");
string approved = Path.Combine(root, "tests", "Rustaveli.Pdf.ConformanceTests", "Snapshots");
string prefix = args.Length > 0 ? args[0] : string.Empty;

Directory.CreateDirectory(approved);

if (!Directory.Exists(received))
{
    Console.WriteLine("Nothing to approve: no received snapshots.");
    return 0;
}

int count = 0;
foreach (string file in Directory.EnumerateFiles(received, "*.received.png"))
{
    string name = Path.GetFileName(file)[..^".received.png".Length];
    if (!name.StartsWith(prefix, StringComparison.Ordinal))
        continue;

    File.Copy(file, Path.Combine(approved, $"{name}.png"), overwrite: true);
    File.Delete(file);
    File.Delete(Path.Combine(received, $"{name}.diff.png"));
    Console.WriteLine($"approved {name}");
    count++;
}

Console.WriteLine($"{count} snapshot(s) approved.");
return 0;
