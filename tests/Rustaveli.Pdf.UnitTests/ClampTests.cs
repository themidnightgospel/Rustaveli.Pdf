namespace Rustaveli.Pdf.UnitTests;

public class ClampTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(5f, 5f)]
    [InlineData(12f, 10f)]
    public void AValueIsKeptBetweenItsBounds(float value, float kept) =>
        Assert.Equal(kept, Clamp.Between(value, 0, 10));

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1.5f, 1f)]
    public void AFractionIsKeptFromNothingToWhole(float value, float kept) =>
        Assert.Equal(kept, Clamp.Fraction(value));

    [Theory]
    [InlineData(-0.5f, 0)]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 128)]
    [InlineData(1f, 255)]
    [InlineData(1.5f, 255)]
    public void AFractionBecomesTheNearestByteKeptFromNothingToWhole(float fraction, byte value) =>
        Assert.Equal(value, Clamp.ToByte(fraction));
}
