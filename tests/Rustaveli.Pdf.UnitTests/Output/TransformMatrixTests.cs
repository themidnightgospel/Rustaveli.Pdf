using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests.Output;

/// <summary>
/// The transform the PDF surface keeps in force, which transforms that can each be written may multiply beyond what can.
/// </summary>
public class TransformMatrixTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ATransformWithAnyEntryBeyondAPdfIsNotWritable(int entry)
    {
        double[] values = [1, 0, 0, 1, 0, 0];
        values[entry] = entry % 2 == 0 ? -1e15 : double.NaN;

        Assert.True(Transform.Identity.IsWritable);
        Assert.False(new Transform(values[0], values[1], values[2], values[3], values[4], values[5]).IsWritable);
    }

    [Fact]
    public void WritableTransformsCanMultiplyBeyondAPdf()
    {
        Transform once = Transform.Scaling(1e10, 1e10);

        Assert.True(once.IsWritable);
        Assert.False(once.After(once).IsWritable);
    }
}
