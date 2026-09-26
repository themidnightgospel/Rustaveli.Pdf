using System.Runtime.InteropServices;

namespace Rustaveli.Pdf.Fonts;

/// <summary>Where each operating system keeps installed fonts.</summary>
/// <remarks>
/// Every decision here is a pure function of values passed in, so each platform's folders can be tested on any one
/// machine; <see cref="ForCurrentPlatform"/> only gathers those values from the running system.
/// </remarks>
internal static class SystemFontDirectories
{
    public static IReadOnlyList<string> ForCurrentPlatform() =>
        For(
            Classify(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX)),
            WindowsFontsFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                Environment.GetEnvironmentVariable("WINDIR")),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static FontPlatform Classify(bool isWindows, bool isMacOS) =>
        isWindows ? FontPlatform.Windows : isMacOS ? FontPlatform.MacOS : FontPlatform.Unix;

    /// <summary>
    /// The Windows fonts folder as the shell reports it, or under the Windows directory: server editions without a
    /// shell can report no Fonts folder, though the Windows directory still has one.
    /// </summary>
    public static string? WindowsFontsFolder(string shellFontsFolder, string? windowsDirectory)
    {
        if (shellFontsFolder.Length > 0)
            return shellFontsFolder;

        return string.IsNullOrEmpty(windowsDirectory) ? null : Path.Combine(windowsDirectory!, "Fonts");
    }

    /// <summary>The font folders of <paramref name="platform"/>, most general first.</summary>
    /// <param name="platform">The operating system.</param>
    /// <param name="windowsFonts">The Windows fonts folder, usually C:\Windows\Fonts; ignored elsewhere.</param>
    /// <param name="localApplicationData">The local application data folder, holding per-user Windows fonts.</param>
    /// <param name="home">The user's home directory; ignored on Windows.</param>
    public static IReadOnlyList<string> For(
        FontPlatform platform, string? windowsFonts, string? localApplicationData, string? home)
    {
        List<string> directories = [];

        switch (platform)
        {
            case FontPlatform.Windows:
                AddIfSet(directories, windowsFonts);

                if (!string.IsNullOrEmpty(localApplicationData))
                    directories.Add(Path.Combine(localApplicationData!, "Microsoft", "Windows", "Fonts"));

                break;

            case FontPlatform.MacOS:
                directories.Add("/System/Library/Fonts");
                directories.Add("/Library/Fonts");

                if (!string.IsNullOrEmpty(home))
                    directories.Add(Path.Combine(home!, "Library", "Fonts"));

                break;

            default:
                directories.Add("/usr/share/fonts");
                directories.Add("/usr/local/share/fonts");

                if (!string.IsNullOrEmpty(home))
                {
                    directories.Add(Path.Combine(home!, ".local", "share", "fonts"));
                    directories.Add(Path.Combine(home!, ".fonts"));
                }

                break;
        }

        return directories;
    }

    private static void AddIfSet(List<string> directories, string? directory)
    {
        if (!string.IsNullOrEmpty(directory))
            directories.Add(directory!);
    }
}
