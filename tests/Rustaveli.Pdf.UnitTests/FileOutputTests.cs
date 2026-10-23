using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests;

public class FileOutputTests
{
    [Fact]
    public async Task AFileHeldOpenForAMomentIsStillMovedWhereNothingIsYet()
    {
        // As a virus scanner holds a file the moment the handle that wrote it closes.
        string partial = Path.Combine(Path.GetTempPath(), "output-" + Guid.NewGuid().ToString("N") + ".partial");
        string target = Path.ChangeExtension(partial, ".pdf");

        try
        {
            File.WriteAllText(partial, "contents");
            FileStream held = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.None);
            Task release = Task.Delay(100).ContinueWith(_ => held.Dispose(), TaskScheduler.Default);

            FileOutput.MoveIntoPlace(partial, target);
            await release;

            Assert.Equal("contents", File.ReadAllText(target));
            Assert.False(File.Exists(partial));
        }
        finally
        {
            File.Delete(partial);
            File.Delete(target);
        }
    }

    [Theory]
    [InlineData(0x80070020, true)]
    [InlineData(0x80070497, true)]
    [InlineData(0x80070498, true)]
    [InlineData(0x80070021, false)]
    [InlineData(0x80070003, false)]
    [InlineData(0x80070005, false)]
    public void OnlyTheErrorsAHoldGivesAreTriedAgain(uint result, bool held) =>
        Assert.Equal(held, FileOutput.IsHeld(new IOException("Failed", unchecked((int)result))));

    [Fact]
    public void AFailedWriteLeavesNothingBesideTheDestination()
    {
        string directory = Path.Combine(Path.GetTempPath(), "output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                FileOutput.Write(Path.Combine(directory, "file.pdf"), _ => throw new InvalidOperationException()));

            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
