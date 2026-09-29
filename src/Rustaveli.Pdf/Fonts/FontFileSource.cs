namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The bytes of one font file, read from disk on first need and then shared by every face in it, so the faces of a
/// collection used together do not each hold their own copy of what may be tens of megabytes.
/// </summary>
internal sealed class FontFileSource
{
    private Lazy<ReadOnlyMemory<byte>> _data;

    public FontFileSource(string path)
    {
        Path = path;
        _data = ReadOnce(path);
    }

    public FontFileSource(ReadOnlyMemory<byte> data)
    {
        _data = new Lazy<ReadOnlyMemory<byte>>(() => data, LazyThreadSafetyMode.PublicationOnly);
        _ = _data.Value;
    }

    /// <summary>The file the font was found in; null for a font registered from memory or a stream.</summary>
    public string? Path { get; }

    /// <summary>The file's bytes, read on first call and kept.</summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    public ReadOnlyMemory<byte> Data
    {
        get
        {
            Lazy<ReadOnlyMemory<byte>> data = Volatile.Read(ref _data);

            try
            {
                return data.Value;
            }
            catch
            {
                // The lazy value keeps its exception, and a file locked by another program for a moment would then
                // lose every face in it for as long as the process runs: the failed read is replaced by a fresh one,
                // tried the next time the bytes are asked for.
                Interlocked.CompareExchange(ref _data, ReadOnce(Path!), data);
                throw;
            }
        }
    }

    public bool IsLoaded => Volatile.Read(ref _data).IsValueCreated;

    /// <summary>One read however many threads ask at once: the file may be large, and the result is kept for good.</summary>
    private static Lazy<ReadOnlyMemory<byte>> ReadOnce(string path) =>
        new Lazy<ReadOnlyMemory<byte>>(() => File.ReadAllBytes(path), LazyThreadSafetyMode.ExecutionAndPublication);
}
