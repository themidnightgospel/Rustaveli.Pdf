namespace Rustaveli.Pdf.UnitTests;

public class UnitTests
{
    [Fact]
    public void ConvertsInchesUsingSeventyTwoPointsPerInch()
    {
        Approximately.Equal(72f, 1f.Inches());
    }

    [Fact]
    public void ConvertsMillimetresAgainstTheInch()
    {
        Approximately.Equal(72f, 25.4f.Millimetres());
    }

    [Fact]
    public void TreatsCentimetresAsTenMillimetres()
    {
        Approximately.Equal(10f.Millimetres(), 1f.Centimetres());
    }

    [Fact]
    public void LeavesPointsUnchanged()
    {
        Approximately.Equal(42f, 42f.Points());
    }
}
