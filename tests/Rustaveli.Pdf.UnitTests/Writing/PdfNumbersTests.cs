using System.Globalization;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfNumbersTests
{
    private static string Integer(long value)
    {
        byte[] buffer = new byte[PdfNumbers.MaxLength];
        int length = PdfNumbers.WriteInteger(value, buffer);
        return Latin1.Text(buffer.AsSpan(0, length));
    }

    private static string Real(double value)
    {
        byte[] buffer = new byte[PdfNumbers.MaxLength];
        int length = PdfNumbers.WriteReal(value, buffer);
        return Latin1.Text(buffer.AsSpan(0, length));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(7L, "7")]
    [InlineData(-42L, "-42")]
    [InlineData(1234567890123L, "1234567890123")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void WritesIntegersInPlainDecimal(long value, string expected)
    {
        Assert.Equal(expected, Integer(value));
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(1.0, "1")]
    [InlineData(-1.0, "-1")]
    [InlineData(0.5, "0.5")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(12.25, "12.25")]
    [InlineData(595.0, "595")]
    [InlineData(0.00001, "0.00001")]
    [InlineData(-0.00001, "-0.00001")]
    [InlineData(0.123456, "0.12346")]
    [InlineData(0.100001, "0.1")]
    [InlineData(1.000009, "1.00001")]
    [InlineData(10.5, "10.5")]
    [InlineData(20.05, "20.05")]
    public void WritesRealsInFixedPointWithTrailingZerosTrimmed(double value, string expected)
    {
        Assert.Equal(expected, Real(value));
    }

    [Theory]
    [InlineData(0.000004, "0")]
    [InlineData(-0.000004, "0")]
    [InlineData(-0.0, "0")]
    [InlineData(0.000005, "0.00001")]
    [InlineData(-0.000005, "-0.00001")]
    [InlineData(0.015625, "0.01563")]
    [InlineData(-0.015625, "-0.01563")]
    [InlineData(1000000.5, "1000001")]
    public void RoundsHalfAwayFromZeroAndNeverWritesNegativeZero(double value, string expected)
    {
        Assert.Equal(expected, Real(value));
    }

    [Theory]
    [InlineData(99.123456, "99.12346")]
    [InlineData(100.123456, "100.1235")]
    [InlineData(999.123456, "999.1235")]
    [InlineData(1000.123456, "1000.123")]
    [InlineData(9999.123456, "9999.123")]
    [InlineData(10000.123456, "10000.12")]
    [InlineData(99999.123456, "99999.12")]
    [InlineData(100000.123456, "100000.1")]
    [InlineData(999999.123456, "999999.1")]
    [InlineData(1000000.123456, "1000000")]
    [InlineData(-1000.123456, "-1000.123")]
    [InlineData(123456789.9, "123456790")]
    [InlineData(999999999999999.0, "999999999999999")]
    public void KeepsSevenSignificantDigitsAboveOneHundred(double value, string expected)
    {
        Assert.Equal(expected, Real(value));
    }

    [Theory]
    [InlineData(99.999994, "99.99999")]
    [InlineData(99.999996, "100")]
    [InlineData(999.99996, "1000")]
    [InlineData(-9.999996, "-10")]
    public void CarriesRoundingIntoTheIntegerPart(double value, string expected)
    {
        Assert.Equal(expected, Real(value));
    }

    [Theory]
    [InlineData(559.28f, "559.28")]
    [InlineData(0.1f, "0.1")]
    [InlineData(72.35f, "72.35")]
    [InlineData(1234.56f, "1234.56")]
    [InlineData(12345.67f, "12345.67")]
    [InlineData(-841.89f, "-841.89")]
    public void WritesWidenedFloatsAsTheValueTheAuthorMeant(float value, string expected)
    {
        Assert.Equal(expected, Real(value));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e15)]
    [InlineData(-1e15)]
    [InlineData(1e300)]
    public void RejectsValuesPdfCannotExpress(double value)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Real(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void AcceptsTheLargestMagnitudeBelowTheLimit()
    {
        Assert.Equal("-999999999999999", Real(-999999999999999.4));
    }

    [Fact]
    public void IgnoresTheCurrentCulture()
    {
        using CultureScope scope = new CultureScope(new CultureInfo("de-DE"));

        Assert.Equal("-1234.5", Real(-1234.5));
        Assert.Equal("-1234567", Integer(-1234567));
    }
}
