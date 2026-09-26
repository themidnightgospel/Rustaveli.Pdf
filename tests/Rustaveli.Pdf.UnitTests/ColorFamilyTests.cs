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

    [Fact]
    public void EqualsItsBaseShadeWhenTheShadeArrivesAsAnObject()
    {
        object shade = Colors.Red.Base;

        Assert.True(Colors.Red.Equals(shade));
    }

    [Fact]
    public void DoesNotEqualAnyOtherShadeArrivingAsAnObject()
    {
        object shade = Colors.Red.Darken1;

        Assert.False(Colors.Red.Equals(shade));
    }

    [Fact]
    public void EqualsItselfAsAnObject()
    {
        object sameFamily = Colors.Red;

        Assert.True(Colors.Red.Equals(sameFamily));
    }

    [Fact]
    public void ComparesAgainstAnotherFamilyByIdentityNotByShade()
    {
        // A family is a lookup table rather than a value, so two tables holding the same shades are still two.
        ColorFamily copy = new ColorFamily(
            "f44336", "ffebee", "ffcdd2", "ef9a9a", "e57373", "ef5350", "e53935", "d32f2f", "c62828", "b71c1c");
        object copyAsObject = copy;

        Assert.Equal(Colors.Red.Base, copy.Base);
        Assert.False(Colors.Red.Equals(copyAsObject));
    }

    [Fact]
    public void DoesNotEqualNullOrUnrelatedObjects()
    {
        Assert.False(Colors.Red.Equals((object?)null));
        Assert.False(Colors.Red.Equals((object)"#FFF44336"));
    }

    [Fact]
    public void HashesLikeItsBaseShade()
    {
        // A family equals its base shade, so the two must land in the same hash bucket.
        Assert.Equal(Colors.Red.Base.GetHashCode(), Colors.Red.GetHashCode());
    }
}
