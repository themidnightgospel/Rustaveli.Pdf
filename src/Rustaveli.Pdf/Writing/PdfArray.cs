using System.Collections;

namespace Rustaveli.Pdf.Writing;

/// <summary>A PDF array. Supports collection initialisers: <c>new PdfArray { 0, 0, 595, 842 }</c>.</summary>
internal sealed class PdfArray : IReadOnlyList<PdfValue>
{
    private readonly List<PdfValue> _items;

    public PdfArray()
    {
        _items = new List<PdfValue>();
    }

    public PdfArray(int capacity)
    {
        _items = new List<PdfValue>(capacity);
    }

    public int Count => _items.Count;

    public PdfValue this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(PdfValue value) => _items.Add(value);

    /// <summary>A struct enumerator, so writing an array does not allocate one.</summary>
    public List<PdfValue>.Enumerator GetEnumerator() => _items.GetEnumerator();

    IEnumerator<PdfValue> IEnumerable<PdfValue>.GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
