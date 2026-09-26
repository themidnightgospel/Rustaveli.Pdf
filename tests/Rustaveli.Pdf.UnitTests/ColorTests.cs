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
    public void ReadsTheFirstDigitOfFourDigitShorthandAsAlpha()
    {
        Assert.Equal(new Color(0xFF, 0x00, 0x33, 0x88), Color.ParseHex("#8F03"));
    }

    [Fact]
    public void AcceptsShorthandWithoutTheHash()
    {
        Assert.Equal(new Color(0xAA, 0xBB, 0xCC), Color.ParseHex("abc"));
    }

    [Fact]
    public void DefaultsToOpaqueWhenNoAlphaGiven()
    {
        Assert.Equal(255, Color.ParseHex("#123456").Alpha);
    }

    [Fact]
    public void KeepsAnExplicitZeroAlpha()
    {
        Assert.Equal(new Color(0xFF, 0, 0, 0), Color.ParseHex("#00FF0000"));
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#1")]
    [InlineData("#12")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    public void RejectsADigitCountOtherThanThreeFourSixOrEight(string hex)
    {
        FormatException exception = Assert.Throws<FormatException>(() => Color.ParseHex(hex));

        Assert.Equal($"'{hex}' is not a valid colour. Expected 3, 4, 6 or 8 hexadecimal digits.", exception.Message);
    }

    [Theory]
    [InlineData("#GGGGGG")]
    [InlineData("#12345G")]
    [InlineData("#XYZ")]
    public void RejectsNonHexadecimalDigits(string hex)
    {
        FormatException exception = Assert.Throws<FormatException>(() => Color.ParseHex(hex));

        Assert.Equal($"'{hex}' is not a valid colour. Expected hexadecimal digits.", exception.Message);
    }

    [Theory]
    [InlineData("#12345 ")]
    [InlineData(" 12345")]
    [InlineData("#AB ")]
    [InlineData("#1234567 ")]
    public void RejectsWhitespaceStandingInForADigit(string hex)
    {
        // A space padding a short value out to a valid length is not a digit. Accepting it would read "#12345 "
        // as #012345 and hand back a colour the caller never wrote.
        FormatException exception = Assert.Throws<FormatException>(() => Color.ParseHex(hex));

        Assert.Equal($"'{hex}' is not a valid colour. Expected hexadecimal digits.", exception.Message);
    }

    [Fact]
    public void RejectsNull()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Color.ParseHex(null!));

        Assert.Equal("hex", exception.ParamName);
    }

    [Fact]
    public void RoundTripsThroughArgb()
    {
        Color original = new Color(12, 34, 56, 78);

        Assert.Equal(original, Color.FromArgb(original.ToArgb()));
    }

    [Fact]
    public void PacksAlphaIntoTheHighByteOfArgb()
    {
        Assert.Equal(0x78123456u, new Color(0x12, 0x34, 0x56, 0x78).ToArgb());
    }

    [Fact]
    public void UnpacksArgbWithAlphaInTheHighByte()
    {
        Assert.Equal(new Color(0x12, 0x34, 0x56, 0x78), Color.FromArgb(0x78123456u));
    }

    [Fact]
    public void IsOpaqueUnlessToldOtherwise()
    {
        Assert.Equal(255, new Color(1, 2, 3).Alpha);
    }

    [Fact]
    public void OnlyZeroAlphaIsTransparent()
    {
        Assert.True(new Color(1, 2, 3, 0).IsTransparent);
        Assert.False(new Color(1, 2, 3, 1).IsTransparent);
    }

    [Fact]
    public void WithAlphaReplacesOnlyTheAlpha()
    {
        Assert.Equal(new Color(1, 2, 3, 4), new Color(1, 2, 3).WithAlpha(4));
    }

    [Fact]
    public void FormatsAsAlphaFirstUppercaseHex()
    {
        Assert.Equal("#780A34FE", new Color(0x0A, 0x34, 0xFE, 0x78).ToString());
    }

    [Fact]
    public void ParsesItsOwnFormattedOutput()
    {
        Color original = new Color(0x0A, 0x34, 0xFE, 0x78);

        Assert.Equal(original, Color.ParseHex(original.ToString()));
    }
}
