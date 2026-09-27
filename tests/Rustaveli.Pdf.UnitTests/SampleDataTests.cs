using System.Text.RegularExpressions;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Stand-in content: the same for the same seed, and shaped like what it stands in for.
/// </summary>
public class SampleDataTests
{
    private static readonly Func<SampleData, string>[] Everything =
    [
        sample => sample.Words(5), sample => sample.Heading(), sample => sample.Sentence(), sample => sample.Query(),
        sample => sample.Paragraph(), sample => sample.Paragraphs(2), sample => sample.PersonName(), sample => sample.EmailAddress(),
        sample => sample.WebAddress(), sample => sample.TelephoneNumber(), sample => sample.Number(), sample => sample.DecimalNumber(),
        sample => sample.Percentage(), sample => sample.Amount(), sample => sample.TimeOfDay(), sample => sample.Date(),
        sample => sample.WrittenDate(), sample => sample.Timestamp(), sample => sample.Ink().ToString(), sample => sample.PaleInk().ToString(),
    ];

    private static List<string> All(int seed)
    {
        SampleData sample = new SampleData(seed);
        return Everything.Select(make => make(sample)).ToList();
    }

    [Fact]
    public void TheSameSeedGivesTheSameSample() =>
        Assert.Equal(All(7), All(7));

    [Fact]
    public void ADifferentSeedGivesADifferentSample() =>
        Assert.NotEqual(All(7), All(8));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    public void WordsAreAsManyAsAskedFor(int count)
    {
        string words = new SampleData().Words(count);

        Assert.Equal(count, words.Length == 0 ? 0 : words.Split(' ').Length);
        Assert.Matches("^[a-z ]*$", words);
    }

    [Fact]
    public void TextIsShapedLikeText()
    {
        SampleData sample = new SampleData(3);

        for (int index = 0; index < 20; index++)
        {
            Assert.Matches(@"^[A-Z][a-z]*( [A-Z][a-z]*){1,3}$", sample.Heading());
            Assert.Matches(@"^[A-Z][a-z]*( [a-z]+){5,14}\.$", sample.Sentence());
            Assert.Matches(@"^[A-Z][a-z]*( [a-z]+){4,11}\?$", sample.Query());

            int sentences = Regex.Matches(sample.Paragraph(), @"\.").Count;
            Assert.InRange(sentences, 3, 7);
        }
    }

    [Fact]
    public void ParagraphsAreSeparatedByLineFeeds()
    {
        Assert.Equal(3, new SampleData().Paragraphs(3).Split('\n').Length);
        Assert.Equal(string.Empty, new SampleData().Paragraphs(0));
    }

    [Fact]
    public void ContactsAreShapedLikeContacts()
    {
        SampleData sample = new SampleData(5);

        for (int index = 0; index < 20; index++)
        {
            Assert.Matches(@"^[A-Z][a-z]+ [A-Z][a-z]+$", sample.PersonName());
            Assert.Matches(@"^[a-z]+\.[a-z]+@example\.(com|org|net)$", sample.EmailAddress());
            Assert.Matches(@"^https://www\.example\.(com|org|net)/[a-z]+$", sample.WebAddress());
            Assert.Matches(@"^\+\d{1,2} \d{3} \d{3} \d{4}$", sample.TelephoneNumber());
        }
    }

    [Fact]
    public void NumbersAndDatesAreShapedLikeThemselves()
    {
        SampleData sample = new SampleData(9);

        for (int index = 0; index < 20; index++)
        {
            Assert.InRange(int.Parse(sample.Number(), System.Globalization.CultureInfo.InvariantCulture), 0, 9999);
            Assert.Matches(@"^\d{1,4}\.\d{2}$", sample.DecimalNumber());
            Assert.Matches(@"^\d{1,3}\.\d%$", sample.Percentage());
            Assert.Matches(@"^€\d{1,3}\.\d{2}$", sample.Amount());
            Assert.Matches(@"^([01]\d|2[0-3]):[0-5]\d$", sample.TimeOfDay());
            Assert.Matches(@"^20[0-2]\d-[01]\d-[0-3]\d$", sample.Date());
            Assert.Matches(@"^\d{1,2} [A-Z][a-z]+ 20[0-2]\d$", sample.WrittenDate());
            Assert.Matches(@"^20[0-2]\d-[01]\d-[0-3]\d ([01]\d|2[0-3]):[0-5]\d$", sample.Timestamp());
        }
    }

    [Fact]
    public void PaleInksAreLighterThanStrongOnes()
    {
        SampleData sample = new SampleData(11);

        for (int index = 0; index < 30; index++)
        {
            Assert.InRange(Lightness(sample.PaleInk()), 0.8f, 1f);
            Assert.InRange(Lightness(sample.Ink()), 0.1f, 0.8f);
        }

        static float Lightness(Ink ink)
        {
            (float red, float green, float blue) = ink.ToRgb();
            return (Math.Max(red, Math.Max(green, blue)) + Math.Min(red, Math.Min(green, blue))) / 2;
        }
    }

    [Fact]
    public void AnImageIsTheSizeAskedFor()
    {
        RasterImage image = new SampleData().Image(40, 25);

        Assert.Equal((40, 25), (image.PixelWidth, image.PixelHeight));
    }

    [Fact]
    public void AnImageOfOnePixelIsAllowed()
    {
        RasterImage image = new SampleData().Image(1, 1);

        Assert.Equal((1, 1), (image.PixelWidth, image.PixelHeight));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void AnImageHasPixels(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleData().Image(width, height));

    [Fact]
    public void CountsCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleData().Words(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleData().Paragraphs(-1));
    }
}
