namespace Rustaveli.Pdf.Fonts;

/// <summary>The operating system families whose font folders differ.</summary>
internal enum FontPlatform
{
    Windows,
    MacOS,

    /// <summary>Linux and the other Unix-likes, which follow the freedesktop.org font locations.</summary>
    Unix
}
