using System.Globalization;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfDateTests
{
    private static string Format(DateTimeOffset value)
    {
        PdfString date = PdfDate.Format(value);
        Assert.Equal(PdfStringForm.Literal, date.Form);
        return Latin1.Text(date.Bytes);
    }

    [Theory]
    [InlineData(0, 0, "+00'00'")]
    [InlineData(4, 0, "+04'00'")]
    [InlineData(-5, 0, "-05'00'")]
    [InlineData(5, 30, "+05'30'")]
    [InlineData(5, 45, "+05'45'")]
    [InlineData(-3, -30, "-03'30'")]
    [InlineData(-9, -30, "-09'30'")]
    [InlineData(14, 0, "+14'00'")]
    [InlineData(-12, 0, "-12'00'")]
    public void WritesTheOffsetTheValueCarries(int hours, int minutes, string offset)
    {
        DateTimeOffset value = new DateTimeOffset(2026, 9, 26, 10, 44, 27, new TimeSpan(hours, minutes, 0));

        Assert.Equal("D:20260926104427" + offset, Format(value));
    }

    [Fact]
    public void PadsEveryFieldWithZeros()
    {
        DateTimeOffset value = new DateTimeOffset(987, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(-61));

        Assert.Equal("D:09870102030405-01'01'", Format(value));
    }

    [Fact]
    public void WritesTheLastSecondOfTheYear()
    {
        DateTimeOffset value = new DateTimeOffset(1999, 12, 31, 23, 59, 59, TimeSpan.Zero);

        Assert.Equal("D:19991231235959+00'00'", Format(value));
    }

    [Fact]
    public void IgnoresTheCurrentCultureAndItsCalendar()
    {
        using CultureScope scope = new CultureScope(new CultureInfo("th-TH"));

        Assert.Equal("D:20260101000000+07'00'", Format(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(7))));
    }
}
