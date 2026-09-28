using SkiaSharp;

namespace Rustaveli.Pdf.ConformanceTests.Rendering;

/// <summary>
/// Compares a rendered page with its approved snapshot. On any failure the received image and a diff are written to
/// artifacts/snapshots, and `dotnet run eng/approve-snapshots.cs` promotes received images once a change is intended.
/// </summary>
internal static class SnapshotAssert
{
    /// <summary>A channel may differ by this much before the pixel counts as changed — anti-aliasing noise.</summary>
    private const int ChannelTolerance = 24;

    /// <summary>The share of changed pixels a page may have and still match.</summary>
    private const double PixelTolerance = 0.002;

    public static void Matches(string name, SKBitmap received)
    {
        string approvedPath = Path.Combine(RepositoryPaths.ApprovedSnapshots, $"{name}.png");

        if (!File.Exists(approvedPath))
        {
            string written = WriteReceived(name, received);
            Assert.Fail($"No approved snapshot for '{name}'. Inspect {written} and approve it with `dotnet run eng/approve-snapshots.cs`.");
        }

        using SKBitmap approved = SKBitmap.Decode(approvedPath);

        if (approved.Width != received.Width || approved.Height != received.Height)
        {
            string written = WriteReceived(name, received);
            Assert.Fail($"'{name}' is {received.Width}×{received.Height} but the approved snapshot is {approved.Width}×{approved.Height}. Received: {written}");
        }

        using SKBitmap diff = new SKBitmap(new SKImageInfo(received.Width, received.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        int changed = 0;

        for (int y = 0; y < received.Height; y++)
        {
            for (int x = 0; x < received.Width; x++)
            {
                SKColor expected = approved.GetPixel(x, y);
                SKColor actual = received.GetPixel(x, y);
                int distance = Math.Max(
                    Math.Abs(expected.Red - actual.Red),
                    Math.Max(Math.Abs(expected.Green - actual.Green), Math.Abs(expected.Blue - actual.Blue)));

                if (distance > ChannelTolerance)
                {
                    changed++;
                    diff.SetPixel(x, y, new SKColor(255, 0, 0));
                }
                else
                {
                    // A faded copy of the page, so the red marks can be located.
                    byte grey = (byte)(215 + ((actual.Red + actual.Green + actual.Blue) / 3 * 40 / 255));
                    diff.SetPixel(x, y, new SKColor(grey, grey, grey));
                }
            }
        }

        double share = (double)changed / (received.Width * received.Height);
        if (share > PixelTolerance)
        {
            string written = WriteReceived(name, received);
            string diffPath = Path.Combine(RepositoryPaths.ReceivedSnapshots, $"{name}.diff.png");
            Save(diff, diffPath);
            Assert.Fail($"'{name}' differs from its approved snapshot in {changed} pixels ({share:P2}). Received: {written}  Diff: {diffPath}");
        }

        // A page that matches has nothing to approve: an image left from an earlier failure would otherwise be
        // promoted over the approved one by the next approval run, unseen. A fresh checkout has no folder for them.
        if (Directory.Exists(RepositoryPaths.ReceivedSnapshots))
        {
            File.Delete(Path.Combine(RepositoryPaths.ReceivedSnapshots, $"{name}.received.png"));
            File.Delete(Path.Combine(RepositoryPaths.ReceivedSnapshots, $"{name}.diff.png"));
        }
    }

    private static string WriteReceived(string name, SKBitmap received)
    {
        string path = Path.Combine(RepositoryPaths.ReceivedSnapshots, $"{name}.received.png");
        Save(received, path);
        return path;
    }

    private static void Save(SKBitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
