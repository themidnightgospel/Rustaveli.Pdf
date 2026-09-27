namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Collects objects for one object stream (ISO 32000-1, 7.5.7) and lays out its decoded content: pairs of object
/// number and offset, then the objects themselves. Reused from one object stream to the next.
/// </summary>
internal sealed class ObjectStreamBuilder : IDisposable
{
    private readonly PdfByteWriter _objects = new PdfByteWriter(4096);
    private readonly PdfByteWriter _content = new PdfByteWriter(4096);
    private readonly List<int> _numbers = new List<int>();
    private readonly List<int> _offsets = new List<int>();

    public int Count => _numbers.Count;

    /// <summary>Appends an object and returns its index within the stream.</summary>
    public int Add(int objectNumber, PdfValue value)
    {
        int offset = _objects.Length;
        _objects.WriteValue(value);

        // Objects are located by offset, but a reader parsing "5" still needs to know where it ends: without the
        // break, an integer followed by another would read as one longer number.
        _objects.WriteByte((byte)'\n');

        _numbers.Add(objectNumber);
        _offsets.Add(offset);
        return _numbers.Count - 1;
    }

    /// <summary>The decoded stream content; <paramref name="first"/> receives the value of <c>/First</c>.</summary>
    public ReadOnlySpan<byte> Build(out int first)
    {
        _content.Clear();
        for (int index = 0; index < _numbers.Count; index++)
        {
            _content.WriteInteger(_numbers[index]);
            _content.WriteInteger(_offsets[index]);
        }

        _content.WriteByte((byte)'\n');
        first = _content.Length;
        _content.Write(_objects.WrittenSpan);
        return _content.WrittenSpan;
    }

    public void Clear()
    {
        _objects.Clear();
        _numbers.Clear();
        _offsets.Clear();
    }

    public void Dispose()
    {
        _objects.Dispose();
        _content.Dispose();
    }
}
