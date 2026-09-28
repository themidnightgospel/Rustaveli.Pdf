namespace Rustaveli.Pdf.UnitTests;

public class NumeralsTests
{
    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    [InlineData(90, "XC")]
    [InlineData(400, "CD")]
    [InlineData(1994, "MCMXCIV")]
    [InlineData(3999, "MMMCMXCIX")]
    public void WritesRomanNumerals(int number, string expected)
    {
        Assert.Equal(expected, Numerals.UpperRoman(number));
        Assert.Equal(expected.ToLowerInvariant(), Numerals.LowerRoman(number));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(4000)]
    public void KeepsArabicWhereRomanHasNoNumeral(int number)
    {
        Assert.Equal(Numerals.Arabic(number), Numerals.UpperRoman(number));
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    public void WritesLettersAsSpreadsheetColumnsRun(int number, string expected)
    {
        Assert.Equal(expected, Numerals.UpperAlpha(number));
        Assert.Equal(expected.ToLowerInvariant(), Numerals.LowerAlpha(number));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void KeepsArabicWhereLettersHaveNone(int number)
    {
        Assert.Equal(Numerals.Arabic(number), Numerals.UpperAlpha(number));
    }

    [Fact]
    public void WritesArabicInDigitsEveryCultureReads()
    {
        // Arabic cultures write the minus sign with a right-to-left mark before it.
        using CultureScope culture = new CultureScope(new System.Globalization.CultureInfo("ar-SA"));

        Assert.Equal("-1234", Numerals.Arabic(-1234));
    }

    [Theory]
    [InlineData(ListNumbering.Arabic, "12")]
    [InlineData(ListNumbering.LowerAlpha, "l")]
    [InlineData(ListNumbering.UpperAlpha, "L")]
    [InlineData(ListNumbering.LowerRoman, "xii")]
    [InlineData(ListNumbering.UpperRoman, "XII")]
    [InlineData(ListNumbering.Bullet, "•")]
    public void FormatsInTheStyleOfAList(ListNumbering numbering, string expected)
    {
        Assert.Equal(expected, Numerals.Format(12, numbering));
    }
}
