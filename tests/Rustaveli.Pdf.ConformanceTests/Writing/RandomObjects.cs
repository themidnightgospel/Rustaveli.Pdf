using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.ConformanceTests.Writing;

/// <summary>
/// Generates arbitrary object graphs from a seed: every kind of value, nested, with awkward names and strings, and
/// references back to objects already written. Seeded so a failure reproduces exactly.
/// </summary>
internal sealed class RandomObjects(Random random, PdfFileWriter writer)
{
    private const string Alphabet = "abcXYZ019 #/()<>[]{}%\\\t\nÿéაბ€\u0001";

    private readonly List<PdfReference> _written = new List<PdfReference>();

    /// <summary>Writes a random object or stream and returns its reference.</summary>
    public PdfReference WriteIndirect()
    {
        PdfReference reference;
        if (random.Next(4) == 0)
        {
            byte[] data = new byte[random.Next(0, 3000)];
            if (random.Next(2) == 0)
                random.NextBytes(data);

            reference = writer.WriteStream(RandomDictionary(0), data, PdfStreamCompression.Auto);
        }
        else
        {
            // An indirect object's value may be anything but a bare reference, which the writer refuses.
            PdfValue value = Value(0);
            while (value.Kind == PdfValueKind.Reference)
                value = Value(0);

            reference = writer.Write(value);
        }

        _written.Add(reference);
        return reference;
    }

    private PdfValue Value(int depth)
    {
        int kinds = depth >= 4 ? 7 : 9;
        return random.Next(kinds) switch
        {
            0 => PdfValue.Null,
            1 => random.Next(2) == 0,
            2 => random.Next(int.MinValue, int.MaxValue),
            3 => (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-6, 9)),
            4 => new PdfName(Text(random.Next(0, 12))),
            5 => RandomString(),
            6 => _written.Count > 0 ? _written[random.Next(_written.Count)] : (PdfValue)random.Next(),
            7 => RandomArray(depth + 1),
            _ => RandomDictionary(depth + 1),
        };
    }

    private PdfString RandomString()
    {
        if (random.Next(3) == 0)
            return PdfString.FromText(Text(random.Next(0, 20)));

        byte[] bytes = new byte[random.Next(0, 40)];
        random.NextBytes(bytes);
        return new PdfString(bytes, random.Next(2) == 0 ? PdfStringForm.Literal : PdfStringForm.Hex);
    }

    private PdfArray RandomArray(int depth)
    {
        PdfArray array = new PdfArray();
        int count = random.Next(0, 6);
        for (int index = 0; index < count; index++)
            array.Add(Value(depth));

        return array;
    }

    private PdfDictionary RandomDictionary(int depth)
    {
        PdfDictionary dictionary = new PdfDictionary();
        int count = random.Next(0, 6);
        for (int index = 0; index < count; index++)
        {
            // The prefix keeps random keys clear of /Length and /Filter, which the writer owns on streams, and of
            // /DecodeParms, which would make qpdf decode data that was never encoded.
            PdfName key = new PdfName("K" + Text(random.Next(0, 6)));
            dictionary[key] = Value(depth);
        }

        return dictionary;
    }

    private string Text(int length)
    {
        char[] characters = new char[length];
        for (int index = 0; index < length; index++)
            characters[index] = Alphabet[random.Next(Alphabet.Length)];

        return new string(characters);
    }
}
