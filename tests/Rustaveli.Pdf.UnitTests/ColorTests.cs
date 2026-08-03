namespace Rustaveli.Pdf.UnitTests;

public class ColorTests
{
    [Theory]
    [InlineData("#FF0000", 255, 0, 0, 255)]
    [InlineData("00FF00", 0, 255, 0, 255)]
    [InlineData("#80FF0000", 255, 0, 0, 128)]
    public void ParsesLongFormHex(string hex, byte red, byte green, byte blue, byte alpha)
    {
        Color color = Color.ParseHex(hex);

        Assert.Equal(new Color(red, green, blue, alpha), color);
    }

    [Fact]
    public void ExpandsShorthandByRepeatingEachDigit()
    {
        Assert.Equal(Color.ParseHex("#AABBCC"), Color.ParseHex("#ABC"));
    }

    [Fact]
    public void DefaultsToOpaqueWhenNoAlphaGiven()
    {
        Assert.Equal(255, Color.ParseHex("#123456").Alpha);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("")]
    [InlineData("#GGGGGG")]
    public void RejectsMalformedInput(string hex)
    {
        Assert.Throws<FormatException>(() => Color.ParseHex(hex));
    }

    [Fact]
    public void RoundTripsThroughArgb()
    {
        Color original = new Color(12, 34, 56, 78);

        Assert.Equal(original, Color.FromArgb(original.ToArgb()));
    }
}
