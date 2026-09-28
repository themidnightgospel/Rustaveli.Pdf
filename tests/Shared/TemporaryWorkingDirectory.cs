namespace Rustaveli.Pdf.Testing;

/// <summary>
/// Runs code that reads and writes files by bare name — examples written as a reader would write them — in a
/// directory of its own, deleted afterwards.
/// </summary>
/// <remarks>
/// The working directory belongs to the whole process, so every test that changes it belongs to
/// <see cref="WorkingDirectoryCollection"/>, which runs alone.
/// </remarks>
public sealed class TemporaryWorkingDirectory : IDisposable
{
    private readonly string _previous = Environment.CurrentDirectory;

    public TemporaryWorkingDirectory()
    {
        Location = Path.Combine(Path.GetTempPath(), "rustaveli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Location);
        Environment.CurrentDirectory = Location;
    }

    /// <summary>The directory the code runs in.</summary>
    public string Location { get; }

    public void Dispose()
    {
        Environment.CurrentDirectory = _previous;

        try
        {
            Directory.Delete(Location, recursive: true);
        }
        catch (IOException)
        {
            // A viewer or scanner still holding a file; the temporary folder is cleaned up eventually.
        }
    }
}
