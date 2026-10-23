using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests;

public class FileOutputTests
{
    [Fact]
    public void AFileHeldOpenForAMomentIsStillMovedWhereNothingIsYet()
    {
        // As a virus scanner holds a file the moment the handle that wrote it closes.
        string partial = Path.Combine(Path.GetTempPath(), "output-" + Guid.NewGuid().ToString("N") + ".partial");
        string target = Path.ChangeExtension(partial, ".pdf");

        try
        {
            File.WriteAllText(partial, "contents");

            using (new HeldFile(partial))
                FileOutput.MoveIntoPlace(partial, target);

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
    [InlineData(0x80070499, true)]
    [InlineData(0x80070021, false)]
    [InlineData(0x80070003, false)]
    [InlineData(0x80070005, false)]
    public void OnlyTheErrorsAHoldGivesAreTriedAgain(uint result, bool held) =>
        Assert.Equal(held, FileOutput.IsHeld(new IOException("Failed", unchecked((int)result))));

    [Fact]
    public void AReplacementThatFailsOnceTheFileReplacedIsGoneIsKeptAndNamed()
    {
        // As ReplaceFile leaves things when it fails with ERROR_UNABLE_TO_MOVE_REPLACEMENT and no backup is asked for.
        string directory = Path.Combine(Path.GetTempPath(), "output-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(directory, "file.pdf");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(target, "old");

            IOException error = Assert.Throws<IOException>(() => FileOutput.Write(
                target,
                stream => stream.WriteByte((byte)'n'),
                (partial, replaced) =>
                {
                    File.Delete(replaced);
                    throw new IOException("Failed", unchecked((int)0x80070498));
                }));

            string kept = Assert.Single(Directory.GetFiles(directory));
            Assert.Contains(kept, error.Message, StringComparison.Ordinal);
            Assert.Equal("n", File.ReadAllText(kept));
            Assert.Equal(unchecked((int)0x80070498), error.InnerException!.HResult);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AReplacementThatFailsWithTheFileReplacedStillThereIsDropped()
    {
        string directory = Path.Combine(Path.GetTempPath(), "output-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(directory, "file.pdf");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(target, "old");
            IOException failure = new IOException("Failed", unchecked((int)0x80070020));

            IOException error = Assert.Throws<IOException>(() => FileOutput.Write(
                target,
                stream => stream.WriteByte((byte)'n'),
                (partial, replaced) => throw failure));

            Assert.Same(failure, error);
            Assert.Equal([target], Directory.GetFiles(directory));
            Assert.Equal("old", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

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
