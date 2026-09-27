// Fetches the external validators the conformance tests run, into artifacts/tools (ignored by git).
//
//     dotnet run eng/tools.cs
//
// Downloads are pinned by version and verified by SHA-256 before being unpacked: against the publisher's manifest
// where there is one, against the hash recorded here where there is not. The tests look here, then on PATH.
//
// qpdf is fetched on Windows only; on Linux and macOS it comes from the package manager. veraPDF, which runs on Java, is
// fetched everywhere; on Windows a Java runtime is fetched for it too, elsewhere the one on PATH or JAVA_HOME is used.

#:property PublishAot=false

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

const string QpdfVersion = "12.4.1";
const string JreRelease = "jdk-21.0.12.1+1";
const string JreAsset = "OpenJDK21U-jre_x64_windows_hotspot_21.0.12.1_1.zip";
const string VeraPdfVersion = "1.30.2";

// veraPDF signs its releases but publishes no hashes, so this one is recorded here, taken from the release itself.
const string VeraPdfSha256 = "6cc6341cb1af644044054b81f00a6590a7918abb18f762243de115258bcad838";

string root = RepositoryRoot();
string tools = Path.Combine(root, "artifacts", "tools");
bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture == Architecture.X64;

using HttpClient http = new HttpClient();

if (windows)
    await FetchQpdf();
else
    Console.WriteLine("qpdf: install it with your package manager, apt-get install qpdf or brew install qpdf.");

string java = windows ? await FetchJava() : FindJava();
await FetchVeraPdf(java);
return 0;

// The Linux archive stores its shared library as a symbolic link, which ZIP extraction writes out as a small plain file,
// so the binary cannot load; Linux and macOS use their package managers' qpdf instead.
async Task FetchQpdf()
{
    string asset = $"qpdf-{QpdfVersion}-msvc64.zip";
    string target = Path.Combine(tools, $"qpdf-{QpdfVersion}");

    if (Directory.Exists(target))
    {
        Console.WriteLine($"qpdf {QpdfVersion} already present at {target}");
        return;
    }

    string baseUrl = $"https://github.com/qpdf/qpdf/releases/download/v{QpdfVersion}";
    byte[] archive = await Download($"{baseUrl}/{asset}");
    string manifest = await http.GetStringAsync($"{baseUrl}/qpdf-{QpdfVersion}.sha256");
    Verify(asset, archive, Listed(manifest, asset));
    Unzip(archive, target);
    Console.WriteLine($"qpdf {QpdfVersion} verified and unpacked to {target}");
}

async Task<string> FetchJava()
{
    string target = Path.Combine(tools, JreRelease);
    string executable = Path.Combine(target, JreRelease + "-jre", "bin", "java.exe");

    if (File.Exists(executable))
    {
        Console.WriteLine($"Java {JreRelease} already present at {target}");
        return executable;
    }

    string url = $"https://github.com/adoptium/temurin21-binaries/releases/download/{Uri.EscapeDataString(JreRelease)}/{JreAsset}";
    byte[] archive = await Download(url);
    string manifest = await http.GetStringAsync(url + ".sha256.txt");
    Verify(JreAsset, archive, Listed(manifest, JreAsset));
    Unzip(archive, target);
    Console.WriteLine($"Java {JreRelease} verified and unpacked to {target}");
    return executable;
}

// veraPDF ships as an installer, run here without a window, choosing the command line and nothing else.
async Task FetchVeraPdf(string javaExecutable)
{
    string target = Path.Combine(tools, $"verapdf-{VeraPdfVersion}");

    if (Directory.Exists(Path.Combine(target, "bin")))
    {
        Console.WriteLine($"veraPDF {VeraPdfVersion} already present at {target}");
        return;
    }

    string asset = $"verapdf-greenfield-{VeraPdfVersion}-installer.zip";
    string minor = VeraPdfVersion.Substring(0, VeraPdfVersion.LastIndexOf('.'));
    byte[] archive = await Download($"https://software.verapdf.org/rel/{minor}/{asset}");
    Verify(asset, archive, VeraPdfSha256);

    string staging = Path.Combine(tools, $"verapdf-installer-{VeraPdfVersion}");
    Unzip(archive, staging);

    string installer = Directory.EnumerateFiles(staging, "verapdf-izpack-installer-*.jar", SearchOption.AllDirectories).Single();
    string answers = Path.Combine(staging, "auto-install.xml");
    File.WriteAllText(answers, $"""
        <?xml version="1.0" encoding="UTF-8" standalone="no"?>
        <AutomatedInstallation langpack="eng">
            <com.izforge.izpack.panels.htmlhello.HTMLHelloPanel id="welcome"/>
            <com.izforge.izpack.panels.target.TargetPanel id="install_dir">
                <installpath>{target}</installpath>
            </com.izforge.izpack.panels.target.TargetPanel>
            <com.izforge.izpack.panels.packs.PacksPanel id="sdk_pack_select">
                <pack index="0" name="veraPDF GUI" selected="true"/>
                <pack index="1" name="veraPDF Batch files" selected="true"/>
                <pack index="2" name="veraPDF Validation model" selected="false"/>
                <pack index="3" name="veraPDF Documentation" selected="false"/>
                <pack index="4" name="veraPDF Sample Plugins" selected="false"/>
            </com.izforge.izpack.panels.packs.PacksPanel>
            <com.izforge.izpack.panels.install.InstallPanel id="install"/>
            <com.izforge.izpack.panels.finish.FinishPanel id="finish"/>
        </AutomatedInstallation>
        """);

    ProcessStartInfo start = new ProcessStartInfo(javaExecutable) { UseShellExecute = false };
    start.ArgumentList.Add("-jar");
    start.ArgumentList.Add(installer);
    start.ArgumentList.Add(answers);

    using Process process = Process.Start(start)!;
    process.WaitForExit();

    if (process.ExitCode != 0 || !Directory.Exists(Path.Combine(target, "bin")))
        throw new InvalidOperationException($"The veraPDF installer failed with exit code {process.ExitCode}.");

    Directory.Delete(staging, recursive: true);
    Console.WriteLine($"veraPDF {VeraPdfVersion} verified and installed to {target}");
}

async Task<byte[]> Download(string url)
{
    Console.WriteLine($"downloading {url}");
    return await http.GetByteArrayAsync(url);
}

static string? Listed(string manifest, string asset) => manifest
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == asset)
    .Select(parts => parts[0])
    .FirstOrDefault();

static void Verify(string asset, byte[] archive, string? expected)
{
    string actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

    if (expected == null || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"checksum mismatch for {asset}: expected {expected ?? "(not listed)"}, got {actual}");
}

static void Unzip(byte[] archive, string target)
{
    Directory.CreateDirectory(target);
    using MemoryStream stream = new MemoryStream(archive);
    using ZipArchive zip = new ZipArchive(stream);
    zip.ExtractToDirectory(target, overwriteFiles: true);
}

// The Java on the runner or machine: JAVA_HOME's, else the first on PATH.
static string FindJava()
{
    string? home = Environment.GetEnvironmentVariable("JAVA_HOME");
    if (!string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, "bin", "java")))
        return Path.Combine(home, "bin", "java");

    return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory, "java"))
        .FirstOrDefault(File.Exists)
        ?? throw new InvalidOperationException("veraPDF needs Java: install a Java runtime, 11 or later, or set JAVA_HOME.");
}

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
