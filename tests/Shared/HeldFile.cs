namespace Rustaveli.Pdf.Testing;

/// <summary>
/// A file held open with no sharing for a moment, as a virus scanner holds one it has just seen written. It is let go
/// of by a thread of its own: a test that blocks while the file is held must not wait on the thread pool to let it go,
/// which other tests can keep busy for longer than the moment lasts.
/// </summary>
internal sealed class HeldFile : IDisposable
{
    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(100);

    private readonly ManualResetEventSlim _letGo = new ManualResetEventSlim();
    private readonly Thread _holder;

    public HeldFile(string path)
    {
        FileStream held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        _holder = new Thread(() =>
        {
            _letGo.Wait(Moment);
            held.Dispose();
        })
        { IsBackground = true };

        _holder.Start();
    }

    /// <summary>Lets the file go now if the moment is not over, so a test that failed can still delete it.</summary>
    public void Dispose()
    {
        _letGo.Set();
        _holder.Join();
        _letGo.Dispose();
    }
}
