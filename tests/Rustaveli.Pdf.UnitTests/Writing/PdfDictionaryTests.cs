using System.Collections;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfDictionaryTests
{
    private static readonly PdfName A = new PdfName("A");
    private static readonly PdfName B = new PdfName("B");
    private static readonly PdfName C = new PdfName("C");

    [Fact]
    public void WritesEntriesInTheOrderTheyWereFirstAdded()
    {
        PdfDictionary dictionary = new PdfDictionary(1) { [C] = 3, [A] = 1 };
        dictionary.Add(B, 2);

        Assert.Equal(3, dictionary.Count);
        Assert.Equal("<</C 3/A 1/B 2>>", Latin1.Written(writer => writer.WriteDictionary(dictionary)));
    }

    [Fact]
    public void ReplacesAnExistingEntryInPlace()
    {
        PdfDictionary dictionary = new PdfDictionary { [A] = 1, [B] = 2 };

        dictionary[new PdfName("A")] = 10;

        Assert.Equal(2, dictionary.Count);
        Assert.Equal("<</A 10/B 2>>", Latin1.Written(writer => writer.WriteDictionary(dictionary)));
    }

    [Fact]
    public void RefusesToAddADuplicateKey()
    {
        PdfDictionary dictionary = new PdfDictionary { [A] = 1 };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => dictionary.Add(new PdfName("A"), 2));

        Assert.Equal("key", exception.ParamName);
        Assert.Equal(1, dictionary[A].AsInteger());
    }

    [Fact]
    public void LooksEntriesUp()
    {
        PdfDictionary dictionary = new PdfDictionary { [A] = 1, [B] = 2 };

        Assert.True(dictionary.ContainsKey(B));
        Assert.False(dictionary.ContainsKey(C));
        Assert.True(dictionary.TryGetValue(B, out PdfValue found));
        Assert.Equal(2, found.AsInteger());
        Assert.Equal(1, dictionary[A].AsInteger());
    }

    [Fact]
    public void ReportsAMissingEntry()
    {
        PdfDictionary dictionary = new PdfDictionary { [A] = 1 };

        Assert.False(dictionary.TryGetValue(C, out PdfValue missing));
        Assert.Equal(PdfValueKind.Null, missing.Kind);
        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => dictionary[C]);
        Assert.Equal("The dictionary has no /C entry.", exception.Message);
    }

    [Fact]
    public void RefusesNullKeys()
    {
        PdfDictionary dictionary = new PdfDictionary();

        Assert.Throws<ArgumentNullException>(() => dictionary[null!] = 1);
        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null!, 1));
        Assert.Throws<ArgumentNullException>(() => dictionary.ContainsKey(null!));
    }

    [Fact]
    public void EnumeratesThroughEveryInterface()
    {
        PdfDictionary dictionary = new PdfDictionary { [A] = 1, [B] = 2 };

        List<string> generic = ((IEnumerable<KeyValuePair<PdfName, PdfValue>>)dictionary).Select(entry => entry.Key.Value).ToList();
        List<string> plain = new List<string>();
        foreach (object entry in (IEnumerable)dictionary)
            plain.Add(((KeyValuePair<PdfName, PdfValue>)entry).Key.Value);

        Assert.Equal(new[] { "A", "B" }, generic);
        Assert.Equal(new[] { "A", "B" }, plain);
    }
}
