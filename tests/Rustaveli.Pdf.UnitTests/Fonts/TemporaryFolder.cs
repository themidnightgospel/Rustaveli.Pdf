namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>A folder of its own under the system temporary folder, deleted with everything in it on disposal.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    public TemporaryFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rustaveli-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes a file at a path relative to the folder, creating subfolders as needed.</summary>
    public string Write(string relativePath, byte[] contents)
    {
        string path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A file still open elsewhere; the system cleans its temporary folder eventually.
        }
    }
}
