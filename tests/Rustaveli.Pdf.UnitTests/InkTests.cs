namespace Rustaveli.Pdf.UnitTests;

public class InkTests
{
    private const float Tolerance = 0.0001f;

    private static void AssertRgb(Ink ink, float red, float green, float blue)
    {
        (float actualRed, float actualGreen, float actualBlue) = ink.ToRgb();
        Assert.Equal(red, actualRed, Tolerance);
        Assert.Equal(green, actualGreen, Tolerance);
        Assert.Equal(blue, actualBlue, Tolerance);
    }

    private static void AssertCmyk(Ink ink, float cyan, float magenta, float yellow, float black)
    {
        (float actualCyan, float actualMagenta, float actualYellow, float actualBlack) = ink.ToCmyk();
        Assert.Equal(cyan, actualCyan, Tolerance);
        Assert.Equal(magenta, actualMagenta, Tolerance);
        Assert.Equal(yellow, actualYellow, Tolerance);
        Assert.Equal(black, actualBlack, Tolerance);
    }

    // ---- Creating inks -----------------------------------------------------------------------------------------

    [Fact]
    public void AnRgbInkKeepsItsChannelsAsFractions()
    {
        Ink ink = Ink.Rgb(255, 51, 0);

        Assert.Equal(InkModel.Rgb, ink.Model);
        AssertRgb(ink, 1f, 0.2f, 0f);
        Assert.Equal(1f, ink.Opacity);
        Assert.Null(ink.SpotName);
        Assert.Equal(InkModel.Rgb, ink.FallbackModel);
        Assert.Equal(1f, ink.SpotTint);
    }

    [Fact]
    public void ACmykInkKeepsItsComponents()
    {
        Ink ink = Ink.Cmyk(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.Equal(InkModel.Cmyk, ink.Model);
        AssertCmyk(ink, 0.1f, 0.2f, 0.3f, 0.4f);
        Assert.Equal(InkModel.Cmyk, ink.FallbackModel);
    }

    [Theory]
    [InlineData(-0.01f, 0, 0, 0, "cyan")]
    [InlineData(0, 1.01f, 0, 0, "magenta")]
    [InlineData(0, 0, float.NaN, 0, "yellow")]
    [InlineData(0, 0, 0, float.PositiveInfinity, "black")]
    public void CmykComponentsMustBeFractions(float cyan, float magenta, float yellow, float black, string parameter)
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => Ink.Cmyk(cyan, magenta, yellow, black));

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void CmykAcceptsBothEndsOfTheRange()
    {
        AssertCmyk(Ink.Cmyk(0, 0, 0, 0), 0, 0, 0, 0);
        AssertCmyk(Ink.Cmyk(1, 1, 1, 1), 1, 1, 1, 1);
    }

    [Fact]
    public void ASpotInkCarriesItsNameAndFallback()
    {
        Ink ink = Ink.Spot("PANTONE 871 C", Ink.Cmyk(0.2f, 0.3f, 0.7f, 0.1f));

        Assert.Equal(InkModel.Spot, ink.Model);
        Assert.Equal("PANTONE 871 C", ink.SpotName);
        Assert.Equal(InkModel.Cmyk, ink.FallbackModel);
        Assert.Equal(1f, ink.SpotTint);
        AssertCmyk(ink, 0.2f, 0.3f, 0.7f, 0.1f);
    }

    [Fact]
    public void ASpotInkCanFallBackToRgb()
    {
        Ink ink = Ink.Spot("Brand Blue", Ink.Rgb(0, 51, 255));

        Assert.Equal(InkModel.Rgb, ink.FallbackModel);
        AssertRgb(ink, 0f, 0.2f, 1f);
    }

    [Fact]
    public void ASpotInkTakesItsFallbacksOpacity()
    {
        Ink ink = Ink.Spot("Varnish", Ink.Rgb(0, 0, 0).WithOpacity(0.5f));

        Assert.Equal(0.5f, ink.Opacity);
    }

    [Fact]
    public void ASpotInkCannotFallBackToAnotherSpotInk()
    {
        Ink other = Ink.Spot("First", Ink.Black);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Ink.Spot("Second", other));

        Assert.Equal("fallback", exception.ParamName);
        Assert.StartsWith("A spot ink's fallback must be an RGB or CMYK colour", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ASpotInkNeedsAName(string name)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Ink.Spot(name, Ink.Black));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void ASpotInkRejectsANullName()
    {
        Assert.Throws<ArgumentNullException>("name", () => Ink.Spot(null!, Ink.Black));
    }

    // ---- Named inks --------------------------------------------------------------------------------------------

    [Fact]
    public void BlackIsBlackInkAlone()
    {
        Assert.Equal(InkModel.Cmyk, Ink.Black.Model);
        AssertCmyk(Ink.Black, 0, 0, 0, 1);
        AssertRgb(Ink.Black, 0, 0, 0);
    }

    [Fact]
    public void WhiteIsNoInkAtAll()
    {
        Assert.Equal(InkModel.Cmyk, Ink.White.Model);
        AssertCmyk(Ink.White, 0, 0, 0, 0);
        AssertRgb(Ink.White, 1, 1, 1);
    }

    [Fact]
    public void TransparentHasNoOpacity()
    {
        Assert.True(Ink.Transparent.IsTransparent);
        Assert.Equal(0f, Ink.Transparent.Opacity);
    }

    [Fact]
    public void RegistrationPrintsOnEverySeparation()
    {
        Assert.Equal(InkModel.Spot, Ink.Registration.Model);
        Assert.Equal("All", Ink.Registration.SpotName);
        AssertCmyk(Ink.Registration, 1, 1, 1, 1);
    }

    // ---- Hex -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("#FF0000", 1f, 0f, 0f, 1f)]
    [InlineData("00FF00", 0f, 1f, 0f, 1f)]
    [InlineData("#80FF0000", 1f, 0f, 0f, 128 / 255f)]
    [InlineData("#abcdef", 0xAB / 255f, 0xCD / 255f, 0xEF / 255f, 1f)]
    public void ParsesLongFormHex(string hex, float red, float green, float blue, float opacity)
    {
        Ink ink = Ink.Hex(hex);

        Assert.Equal(InkModel.Rgb, ink.Model);
        AssertRgb(ink, red, green, blue);
        Assert.Equal(opacity, ink.Opacity, Tolerance);
    }

    [Fact]
    public void ExpandsShorthandByRepeatingEachDigit()
    {
        Assert.Equal(Ink.Hex("#AABBCC"), Ink.Hex("#ABC"));
    }

    [Fact]
    public void ReadsTheFirstDigitOfFourDigitShorthandAsAlpha()
    {
        Ink ink = Ink.Hex("#8F00");

        Assert.Equal(0x88 / 255f, ink.Opacity, Tolerance);
        AssertRgb(ink, 1f, 0f, 0f);
    }

    [Fact]
    public void KeepsAnExplicitZeroAlpha()
    {
        Assert.True(Ink.Hex("#00FF0000").IsTransparent);
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
        FormatException exception = Assert.Throws<FormatException>(() => Ink.Hex(hex));

        Assert.Equal($"'{hex}' is not a valid colour. Expected 3, 4, 6 or 8 hexadecimal digits.", exception.Message);
    }

    [Theory]
    [InlineData("#GGGGGG")]
    [InlineData("#12345G")]
    [InlineData("#XYZ")]
    public void RejectsNonHexadecimalDigits(string hex)
    {
        FormatException exception = Assert.Throws<FormatException>(() => Ink.Hex(hex));

        Assert.Equal($"'{hex}' is not a valid colour. Expected hexadecimal digits.", exception.Message);
    }

    [Theory]
    [InlineData("#12345 ")]
    [InlineData(" 12345")]
    [InlineData("#AB ")]
    [InlineData("#1234567 ")]
    public void RejectsWhitespaceStandingInForADigit(string hex)
    {
        Assert.Throws<FormatException>(() => Ink.Hex(hex));
    }

    [Fact]
    public void HexRejectsNull()
    {
        Assert.Throws<ArgumentNullException>("hex", () => Ink.Hex(null!));
    }

    // ---- Tints -----------------------------------------------------------------------------------------------

    [Fact]
    public void ATintOfAnRgbColourMovesItTowardWhitePaper()
    {
        Ink tinted = Ink.Rgb(0, 51, 255).Tint(0.5f);

        Assert.Equal(InkModel.Rgb, tinted.Model);
        AssertRgb(tinted, 0.5f, 0.6f, 1f);
    }

    [Fact]
    public void ATintOfAProcessColourScalesEachComponent()
    {
        Ink tinted = Ink.Cmyk(0.2f, 0.4f, 0.6f, 1f).Tint(0.25f);

        Assert.Equal(InkModel.Cmyk, tinted.Model);
        AssertCmyk(tinted, 0.05f, 0.1f, 0.15f, 0.25f);
    }

    [Fact]
    public void ATintOfASpotInkIsTheTintItsPlatePrintsAt()
    {
        Ink tinted = Ink.Spot("Gold", Ink.Cmyk(0, 0.2f, 0.8f, 0)).Tint(0.4f);

        Assert.Equal(InkModel.Spot, tinted.Model);
        Assert.Equal("Gold", tinted.SpotName);
        Assert.Equal(0.4f, tinted.SpotTint, Tolerance);
        AssertCmyk(tinted, 0, 0.08f, 0.32f, 0);
        AssertRgb(tinted, 1f, 1 - (0.4f * 0.2f), 1 - (0.4f * 0.8f));
    }

    [Fact]
    public void TintsCompound()
    {
        Assert.Equal(0.25f, Ink.Spot("Gold", Ink.Black).Tint(0.5f).Tint(0.5f).SpotTint, Tolerance);
    }

    [Fact]
    public void AFullTintIsTheInkItselfAndNoTintIsBarePaper()
    {
        Ink ink = Ink.Rgb(10, 20, 30);

        Assert.Equal(ink, ink.Tint(1));
        AssertRgb(ink.Tint(0), 1, 1, 1);
        AssertCmyk(Ink.Cmyk(0.5f, 0.5f, 0.5f, 0.5f).Tint(0), 0, 0, 0, 0);
    }

    [Fact]
    public void TintingKeepsTheOpacity()
    {
        Assert.Equal(0.3f, Ink.Rgb(0, 0, 0).WithOpacity(0.3f).Tint(0.5f).Opacity, Tolerance);
        Assert.Equal(0.3f, Ink.Cmyk(0, 0, 0, 1).WithOpacity(0.3f).Tint(0.5f).Opacity, Tolerance);
        Assert.Equal(0.3f, Ink.Spot("S", Ink.Black.WithOpacity(0.3f)).Tint(0.5f).Opacity, Tolerance);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    public void ATintMustBeAFraction(float amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>("amount", () => Ink.Black.Tint(amount));
    }

    // ---- Opacity ---------------------------------------------------------------------------------------------

    [Fact]
    public void WithOpacityChangesOnlyTheOpacity()
    {
        Ink ink = Ink.Spot("Gold", Ink.Cmyk(0, 0.2f, 0.8f, 0)).Tint(0.5f);

        Ink faded = ink.WithOpacity(0.25f);

        Assert.Equal(0.25f, faded.Opacity);
        Assert.Equal(ink.Model, faded.Model);
        Assert.Equal(ink.SpotName, faded.SpotName);
        Assert.Equal(ink.SpotTint, faded.SpotTint);
        Assert.Equal(ink.ToCmyk(), faded.ToCmyk());
        Assert.False(faded.IsTransparent);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void OpacityMustBeAFraction(float opacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>("opacity", () => Ink.Black.WithOpacity(opacity));
    }

    // ---- Conversions -----------------------------------------------------------------------------------------

    [Fact]
    public void ProcessColourConvertsToRgbWithoutAProfile()
    {
        AssertRgb(Ink.Cmyk(0.5f, 0.25f, 0, 0.2f), 0.4f, 0.6f, 0.8f);
    }

    [Fact]
    public void RgbConvertsToProcessColourWithBlackFromTheDarkestChannel()
    {
        AssertCmyk(Ink.Rgb(51, 102, 204), 0.75f, 0.5f, 0f, 0.2f);
    }

    [Fact]
    public void PureRgbBlackConvertsToBlackInkAlone()
    {
        AssertCmyk(Ink.Rgb(0, 0, 0), 0, 0, 0, 1);
    }

    [Fact]
    public void RgbWhiteConvertsToNoInk()
    {
        AssertCmyk(Ink.Rgb(255, 255, 255), 0, 0, 0, 0);
    }

    [Fact]
    public void ASpotInkWithAnRgbFallbackConvertsToProcessColourAtItsTint()
    {
        AssertCmyk(Ink.Spot("S", Ink.Rgb(51, 102, 204)).Tint(0.5f), 0.375f, 0.25f, 0f, 0.1f);
    }

    // ---- Equality and formatting -----------------------------------------------------------------------------

    [Fact]
    public void InksWithTheSameSpecificationAreEqual()
    {
        Ink first = Ink.Spot("Gold", Ink.Cmyk(0, 0.2f, 0.8f, 0)).Tint(0.5f);
        Ink second = Ink.Spot("Gold", Ink.Cmyk(0, 0.2f, 0.8f, 0)).Tint(0.5f);

        Assert.True(first == second);
        Assert.False(first != second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void InksThatLookAlikeButAreSpecifiedDifferentlyAreNotEqual()
    {
        // Black as RGB and black as K are the same on screen and different on a press.
        Assert.NotEqual(Ink.Rgb(0, 0, 0), Ink.Black);
        Assert.NotEqual(Ink.Spot("A", Ink.Black), Ink.Spot("a", Ink.Black));
        Assert.NotEqual(Ink.Spot("A", Ink.Black), Ink.Spot("A", Ink.Black).Tint(0.5f));
        Assert.NotEqual(Ink.Spot("A", Ink.Cmyk(0, 0, 0, 0)), Ink.Spot("A", Ink.Rgb(0, 0, 0)));
        Assert.NotEqual(Ink.Rgb(0, 0, 0), Ink.Rgb(0, 0, 0).WithOpacity(0.5f));
        Assert.False(Ink.Black.Equals("black"));
    }

    [Fact]
    public void EachComponentTakesPartInEquality()
    {
        Ink ink = Ink.Cmyk(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.NotEqual(ink, Ink.Cmyk(0.9f, 0.2f, 0.3f, 0.4f));
        Assert.NotEqual(ink, Ink.Cmyk(0.1f, 0.9f, 0.3f, 0.4f));
        Assert.NotEqual(ink, Ink.Cmyk(0.1f, 0.2f, 0.9f, 0.4f));
        Assert.NotEqual(ink, Ink.Cmyk(0.1f, 0.2f, 0.3f, 0.9f));
    }

    [Fact]
    public void FormatsEachModelLegibly()
    {
        Assert.Equal("#1E3A8A", Ink.Rgb(0x1E, 0x3A, 0x8A).ToString());
        Assert.Equal("cmyk(0%, 20%, 80%, 5%)", Ink.Cmyk(0, 0.2f, 0.8f, 0.05f).ToString());
        Assert.Equal("spot 'Gold' 40%", Ink.Spot("Gold", Ink.Black).Tint(0.4f).ToString());
        Assert.Equal("#000000 at 50% opacity", Ink.Rgb(0, 0, 0).WithOpacity(0.5f).ToString());
    }
}
