// Fetches the external validators the conformance tests run, into artifacts/tools (ignored by git).
//
//     dotnet run eng/tools.cs
//
// Downloads are pinned by version and verified against the publisher's SHA-256 manifest before being unpacked.
// The tests look here, then on PATH; CI on macOS installs qpdf with Homebrew instead.

#:property PublishAot=false

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

const string QpdfVersion = "12.4.1";

string root = RepositoryRoot();
string tools = Path.Combine(root, "artifacts", "tools");

string? asset =
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture == Architecture.X64 ? $"qpdf-{QpdfVersion}-msvc64.zip" :
    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.X64 ? $"qpdf-{QpdfVersion}-bin-linux-x86_64.zip" :
    null;

if (asset == null)
{
    Console.Error.WriteLine("No portable qpdf build for this platform; install qpdf with your package manager (e.g. brew install qpdf).");
    return 1;
}

string target = Path.Combine(tools, $"qpdf-{QpdfVersion}");
if (Directory.Exists(target))
{
    Console.WriteLine($"qpdf {QpdfVersion} already present at {target}");
    return 0;
}

using HttpClient http = new HttpClient();
string baseUrl = $"https://github.com/qpdf/qpdf/releases/download/v{QpdfVersion}";

Console.WriteLine($"downloading {asset}");
byte[] archive = await http.GetByteArrayAsync($"{baseUrl}/{asset}");
string manifest = await http.GetStringAsync($"{baseUrl}/qpdf-{QpdfVersion}.sha256");

string actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
string? expected = manifest
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == asset)
    .Select(parts => parts[0])
    .FirstOrDefault();

if (expected == null || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"checksum mismatch for {asset}: expected {expected ?? "(not listed)"}, got {actual}");
    return 1;
}

Directory.CreateDirectory(target);
using (MemoryStream stream = new MemoryStream(archive))
using (ZipArchive zip = new ZipArchive(stream))
{
    zip.ExtractToDirectory(target);
}

if (!OperatingSystem.IsWindows())
{
    foreach (string binary in Directory.EnumerateFiles(target, "qpdf", SearchOption.AllDirectories))
        File.SetUnixFileMode(binary, File.GetUnixFileMode(binary) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
}

Console.WriteLine($"qpdf {QpdfVersion} verified and unpacked to {target}");
return 0;

// Found by walking up from the working directory, not from this script's location: a file-based app's build output
// is cached and can be shared between checkouts, so a path captured at compile time may name a different one.
static string RepositoryRoot()
{
    for (DirectoryInfo? directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Rustaveli.Pdf.slnx")))
            return directory.FullName;
    }

    throw new InvalidOperationException("Run this from inside the Rustaveli.Pdf repository.");
}
