namespace Rustaveli.Pdf.UnitTests;

public class PaletteTests
{
    [Fact]
    public void FamilyConvertsImplicitlyToItsBaseShade()
    {
        Color asColor = Colors.Red;

        Assert.Equal(Colors.Red.Base, asColor);
    }

    [Fact]
    public void MaterialValuesMatchTheSpecification()
    {
        // Spot-checked against the published Material palette: red 500, 50 and 900.
        Assert.Equal(Color.ParseHex("#f44336"), Colors.Red.Base);
        Assert.Equal(Color.ParseHex("#ffebee"), Colors.Red.Lighten5);
        Assert.Equal(Color.ParseHex("#b71c1c"), Colors.Red.Darken4);
        Assert.Equal(Color.ParseHex("#ff8a80"), Colors.Red.Accent1);
    }

    [Fact]
    public void NeutralFamiliesCarryNoAccents()
    {
        // Grey, blue grey and brown have no accent shades in Material, so they are a plain family.
        Assert.IsNotType<AccentedColorFamily>(Colors.Grey);
        Assert.IsType<AccentedColorFamily>(Colors.Blue);
    }

    [Fact]
    public void EveryShadeIsOpaque()
    {
        Assert.Equal(255, Colors.Teal.Lighten2.Alpha);
        Assert.Equal(255, Colors.BlueGrey.Darken3.Alpha);
    }
}
