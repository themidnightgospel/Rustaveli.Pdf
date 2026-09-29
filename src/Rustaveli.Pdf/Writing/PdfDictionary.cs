using System.Collections;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A PDF dictionary. Entries are written in the order they were first added, so output is deterministic.
/// Supports initialisers: <c>new PdfDictionary { [PdfNames.Type] = PdfNames.Page }</c>.
/// </summary>
/// <remarks>
/// Lookup is a linear scan. PDF dictionaries are small — a page or a font has a dozen entries — and at that size a
/// scan beats hashing, and keeps insertion order without a second structure.
/// </remarks>
internal sealed class PdfDictionary : IReadOnlyCollection<KeyValuePair<PdfName, PdfValue>>
{
    private readonly List<KeyValuePair<PdfName, PdfValue>> _entries;

    public PdfDictionary()
    {
        _entries = new List<KeyValuePair<PdfName, PdfValue>>();
    }

    public PdfDictionary(int capacity)
    {
        _entries = new List<KeyValuePair<PdfName, PdfValue>>(capacity);
    }

    public int Count => _entries.Count;

    /// <summary>Gets an entry, or sets one — replacing the value in place if the key is already present.</summary>
    public PdfValue this[PdfName key]
    {
        get => TryGetValue(key, out PdfValue value)
            ? value
            : throw new KeyNotFoundException($"The dictionary has no {key} entry.");
        set
        {
            int index = IndexOf(key);
            if (index >= 0)
                _entries[index] = new KeyValuePair<PdfName, PdfValue>(key, value);
            else
                _entries.Add(new KeyValuePair<PdfName, PdfValue>(key, value));
        }
    }

    /// <summary>
    /// Adds an entry; throws if the key is already present, since a repeated key is a malformed dictionary.
    /// </summary>
    public void Add(PdfName key, PdfValue value)
    {
        if (IndexOf(key) >= 0)
            throw new ArgumentException($"The dictionary already has a {key} entry.", nameof(key));

        _entries.Add(new KeyValuePair<PdfName, PdfValue>(key, value));
    }

    /// <summary>
    /// Adds an entry whose key the caller knows is not yet present, without the scan <see cref="Add"/> makes to check:
    /// for dictionaries that grow large under keys unique by construction, which the scan would make quadratic.
    /// </summary>
    public void AddUnique(PdfName key, PdfValue value) => _entries.Add(new KeyValuePair<PdfName, PdfValue>(key, value));

    public bool ContainsKey(PdfName key) => IndexOf(key) >= 0;

    public bool TryGetValue(PdfName key, out PdfValue value)
    {
        int index = IndexOf(key);
        value = index >= 0 ? _entries[index].Value : default;
        return index >= 0;
    }

    /// <summary>A struct enumerator, so writing a dictionary does not allocate one.</summary>
    public List<KeyValuePair<PdfName, PdfValue>>.Enumerator GetEnumerator() => _entries.GetEnumerator();

    IEnumerator<KeyValuePair<PdfName, PdfValue>> IEnumerable<KeyValuePair<PdfName, PdfValue>>.GetEnumerator() =>
        _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();

    private int IndexOf(PdfName key)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (int index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Key.Equals(key))
                return index;
        }

        return -1;
    }
}
