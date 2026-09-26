using CsCheck;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Random input for the writer's serialisers: reals across every magnitude it accepts, text heavy in delimiters and
/// non-ASCII, and arbitrary graphs of PDF values.
/// </summary>
internal static class WriterGenerators
{
    /// <summary>Characters weighted towards the ones that need escaping somewhere: delimiters, '#', controls, non-ASCII.</summary>
    public static Gen<char> Character { get; } = Gen.Frequency(
        (3, Gen.Char["abcXYZ019_.-+"]),
        (3, Gen.Char["()<>[]{}/%#\\ \t\r\n"]),
        (1, Gen.Char['\u0001', 'ÿ']),
        (1, Gen.Char['Ā', '퟿']));

    public static Gen<double> Real { get; } = Gen.OneOf(
        Gen.Double[-1, 1],
        Gen.Double[-1_000, 1_000],
        Gen.Double[-1e7, 1e7],
        Gen.Double[-9.9e14, 9.9e14],
        Gen.Int[-1_000_000, 1_000_000].Select(thousandths => thousandths / 1000.0),
        Gen.Float.Select(value => (double)value).Where(value => Math.Abs(value) < 9.9e14));

    public static Gen<PdfValue> Value { get; } = ValueUpTo(3);

    public static Gen<string> Text(int maximumLength) =>
        Character.Array[0, maximumLength].Select(characters => new string(characters));

    private static Gen<PdfValue> ValueUpTo(int depth)
    {
        Gen<PdfValue> scalar = Gen.OneOf(
            Gen.Const(PdfValue.Null),
            Gen.Bool.Select(value => (PdfValue)value),
            Gen.Long[-1_000_000_000_000, 1_000_000_000_000].Select(value => (PdfValue)value),
            Real.Select(value => (PdfValue)value),
            Text(12).Select(value => (PdfValue)new PdfName(value)),
            Gen.Select(Gen.Byte.Array[0, 40], Gen.Bool, (bytes, hex) => (PdfValue)new PdfString(bytes, hex ? PdfStringForm.Hex : PdfStringForm.Literal)),
            Text(20).Select(value => (PdfValue)PdfString.FromText(value)),
            Gen.Int[1, 100_000].Select(number => (PdfValue)new PdfReference(number)));

        if (depth == 0)
            return scalar;

        Gen<PdfValue> child = ValueUpTo(depth - 1);
        Gen<PdfValue> array = child.List[0, 5].Select(items =>
        {
            PdfArray value = new PdfArray();
            foreach (PdfValue item in items)
                value.Add(item);

            return (PdfValue)value;
        });

        Gen<PdfValue> dictionary = Gen.Select(Text(8), child).List[0, 5].Select(entries =>
        {
            PdfDictionary value = new PdfDictionary();
            foreach ((string key, PdfValue item) in entries)
                value[new PdfName(key)] = item;

            return (PdfValue)value;
        });

        return Gen.Frequency((4, scalar), (1, array), (1, dictionary));
    }
}
