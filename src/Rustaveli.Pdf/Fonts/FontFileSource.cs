namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The bytes of one font file, read from disk on first need and then shared by every face in it, so the faces of a
/// collection used together do not each hold their own copy of what may be tens of megabytes.
/// </summary>
internal sealed class FontFileSource
{
    private readonly Lazy<ReadOnlyMemory<byte>> _data;

    public FontFileSource(string path)
    {
        Path = path;

        // One read however many threads ask at once: the file may be large, and the result is kept for good.
        _data = new Lazy<ReadOnlyMemory<byte>>(
            () => File.ReadAllBytes(path), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public FontFileSource(ReadOnlyMemory<byte> data)
    {
        _data = new Lazy<ReadOnlyMemory<byte>>(() => data, LazyThreadSafetyMode.PublicationOnly);
        _ = _data.Value;
    }

    /// <summary>The file the font was found in; null for a font registered from memory or a stream.</summary>
    public string? Path { get; }

    public ReadOnlyMemory<byte> Data => _data.Value;

    public bool IsLoaded => _data.IsValueCreated;
}
