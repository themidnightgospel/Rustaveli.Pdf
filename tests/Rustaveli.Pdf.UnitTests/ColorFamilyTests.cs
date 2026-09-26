namespace Rustaveli.Pdf.UnitTests;

public class ColorFamilyTests
{
    [Fact]
    public void ConvertingANullFamilyNamesTheProblem()
    {
        // C# applies the conversion to a conditional as a whole rather than per arm, so `flag ? Colors.Red : null`
        // hands a null family to the operator. A named exception beats a bare NullReferenceException from layout.
        ColorFamily? missing = null;

        Assert.Throws<ArgumentNullException>(() => { Color _ = missing!; });
    }

    [Fact]
    public void ReadsAsItsBaseShadeWhenFormatted()
    {
        Assert.Equal(Colors.Red.Base.ToString(), Colors.Red.ToString());
    }

    [Fact]
    public void ComparesEquallyAgainstAColourInEitherDirection()
    {
        // Without an override this was asymmetric: one order compared values, the other compared references.
        Assert.True(Colors.Red.Equals(Colors.Red.Base));
        Assert.True(Colors.Red.Base.Equals(Colors.Red));
    }

    [Fact]
    public void DifferentFamiliesAreNotEqual()
    {
        Assert.False(Colors.Red.Equals(Colors.Blue));
        Assert.False(Colors.Red.Equals(Colors.Blue.Base));
    }
}
