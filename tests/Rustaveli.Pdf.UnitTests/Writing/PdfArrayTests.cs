using System.Collections;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfArrayTests
{
    [Fact]
    public void KeepsItemsInTheOrderAdded()
    {
        PdfArray array = new PdfArray(2) { 1, 2.5, true };

        Assert.Equal(3, array.Count);
        Assert.Equal(1, array[0].AsInteger());
        Assert.Equal(2.5, array[1].AsReal());
        Assert.True(array[2].AsBoolean());
    }

    [Fact]
    public void ReplacesAnItemByIndex()
    {
        PdfArray array = new PdfArray { 1, 2 };

        array[1] = 9;

        Assert.Equal("[1 9]", Latin1.Written(writer => writer.WriteArray(array)));
    }

    [Fact]
    public void EnumeratesThroughEveryInterface()
    {
        PdfArray array = new PdfArray { 1, 2, 3 };

        List<long> generic = ((IEnumerable<PdfValue>)array).Select(item => item.AsInteger()).ToList();
        List<long> plain = new List<long>();
        foreach (object item in (IEnumerable)array)
            plain.Add(((PdfValue)item).AsInteger());

        Assert.Equal(new long[] { 1, 2, 3 }, generic);
        Assert.Equal(new long[] { 1, 2, 3 }, plain);
    }
}
