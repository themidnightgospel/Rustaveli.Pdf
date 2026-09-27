using System.Globalization;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf;

/// <summary>
/// Stand-in content for laying a document out before the real content exists: dummy text as typesetters set it,
/// names, addresses, numbers, dates, inks and images.
/// </summary>
/// <remarks>
/// What it gives depends only on its seed, so a prototype looks the same every time it is made. It is not safe to
/// share between threads; give each its own.
/// </remarks>
public sealed class SampleData
{
    private static readonly string[] Latin =
    [
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod",
        "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "enim", "ad", "minim", "veniam",
        "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo",
        "consequat", "duis", "aute", "irure", "in", "reprehenderit", "voluptate", "velit", "esse", "cillum",
        "fugiat", "nulla", "pariatur", "excepteur", "sint", "occaecat", "cupidatat", "non", "proident", "sunt",
        "culpa", "qui", "officia", "deserunt", "mollit", "anim", "id", "est", "laborum", "curabitur", "pretium",
        "tincidunt", "lacus", "gravida", "orci", "vitae", "nunc", "aliquam", "erat", "volutpat", "morbi", "tristique",
        "senectus", "netus", "malesuada", "fames", "ac", "turpis", "egestas", "vestibulum", "tortor", "quam",
        "feugiat", "ultricies", "mi", "eget", "mauris", "pharetra", "sapien", "faucibus", "semper", "viverra",
    ];

    private static readonly string[] GivenNames =
    [
        "Ana", "Bruno", "Clara", "David", "Elena", "Farid", "Greta", "Hugo", "Irene", "Jonas", "Keira", "Luca",
        "Maya", "Nikola", "Olga", "Pablo", "Qiu", "Rosa", "Sami", "Tamar", "Uma", "Viktor", "Wanda", "Yusuf", "Zoe",
    ];

    private static readonly string[] FamilyNames =
    [
        "Abashidze", "Berg", "Costa", "Dubois", "Eriksen", "Fischer", "Garcia", "Horvat", "Ibrahim", "Jensen",
        "Kowalski", "Lindqvist", "Moreau", "Novak", "Okafor", "Petrov", "Quinn", "Rossi", "Silva", "Tanaka",
        "Urban", "Varga", "Weber", "Yilmaz", "Zimmermann",
    ];

    private static readonly string[] Domains = ["example.com", "example.org", "example.net"];

    private readonly Random _random;

    /// <summary>Sample data that is the same for the same <paramref name="seed"/>.</summary>
    public SampleData(int seed = 0) => _random = new Random(seed);

    /// <summary><paramref name="count"/> dummy words, lower case, separated by spaces.</summary>
    public string Words(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A count of words cannot be negative.");

        string[] words = new string[count];
        for (int index = 0; index < count; index++)
            words[index] = Pick(Latin);

        return string.Join(" ", words);
    }

    /// <summary>A short heading of two to four words, each capitalised.</summary>
    public string Heading()
    {
        string[] words = Words(Between(2, 4)).Split(' ');
        return string.Join(" ", words.Select(Capitalised));
    }

    /// <summary>A sentence of six to fifteen words, capitalised and ending with a full stop.</summary>
    public string Sentence() => Capitalised(Words(Between(6, 15))) + ".";

    /// <summary>A question of five to twelve words.</summary>
    public string Query() => Capitalised(Words(Between(5, 12))) + "?";

    /// <summary>A paragraph of three to seven sentences.</summary>
    public string Paragraph() => string.Join(" ", Enumerable.Range(0, Between(3, 7)).Select(_ => Sentence()));

    /// <summary><paramref name="count"/> paragraphs, separated by line feeds.</summary>
    public string Paragraphs(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A count of paragraphs cannot be negative.");

        return string.Join("\n", Enumerable.Range(0, count).Select(_ => Paragraph()));
    }

    /// <summary>A given name and a family name.</summary>
    public string PersonName() => Pick(GivenNames) + " " + Pick(FamilyNames);

    /// <summary>An email address at one of the domains reserved for examples.</summary>
    public string EmailAddress() => (Pick(GivenNames) + "." + Pick(FamilyNames)).ToLowerInvariant() + "@" + Pick(Domains);

    /// <summary>A web address on one of the domains reserved for examples.</summary>
    public string WebAddress() => "https://www." + Pick(Domains) + "/" + Pick(Latin);

    /// <summary>A telephone number in the international format.</summary>
    public string TelephoneNumber() =>
        string.Format(CultureInfo.InvariantCulture, "+{0} {1:000} {2:000} {3:0000}", Between(1, 99), Between(100, 999), Between(0, 999), Between(0, 9999));

    /// <summary>A whole number from 0 to 9999.</summary>
    public string Number() => Between(0, 9999).ToString(CultureInfo.InvariantCulture);

    /// <summary>A number with two decimals, from 0 to 9999.99.</summary>
    public string DecimalNumber() => (_random.Next(0, 1_000_000) / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A percentage with one decimal.</summary>
    public string Percentage() => (_random.Next(0, 1001) / 10m).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A price in euros, from 1 to 999.99.</summary>
    public string Amount() => "€" + (_random.Next(100, 100_000) / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A time of day on the twenty-four hour clock.</summary>
    public string TimeOfDay() => string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", Between(0, 23), Between(0, 59));

    /// <summary>A date between 2000 and 2029, as <c>yyyy-MM-dd</c>.</summary>
    public string Date() => Day().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A date between 2000 and 2029, written out: <c>7 March 2021</c>.</summary>
    public string WrittenDate() => Day().ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A date and time between 2000 and 2029, as <c>yyyy-MM-dd HH:mm</c>.</summary>
    public string Timestamp() =>
        Day().AddMinutes(Between(0, (24 * 60) - 1)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A strong ink, for text or rules.</summary>
    public Ink Ink() => Hue(0.65f, 0.45f);

    /// <summary>A pale ink, for backgrounds.</summary>
    public Ink PaleInk() => Hue(0.45f, 0.88f);

    /// <summary>
    /// An image of <paramref name="width"/> by <paramref name="height"/> pixels, a soft blend between two pale inks,
    /// to hold the place of a photograph.
    /// </summary>
    public RasterImage Image(int width = 320, int height = 200)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "An image is at least one pixel each way.");

        (float Red, float Green, float Blue) from = PaleInk().ToRgb();
        (float Red, float Green, float Blue) to = PaleInk().ToRgb();
        byte[] pixels = new byte[width * height * 3];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                // Diagonally from the top left to the bottom right.
                float along = ((float)column / Math.Max(1, width - 1) + ((float)row / Math.Max(1, height - 1))) / 2;
                int at = ((row * width) + column) * 3;
                pixels[at] = Channel(from.Red, to.Red, along);
                pixels[at + 1] = Channel(from.Green, to.Green, along);
                pixels[at + 2] = Channel(from.Blue, to.Blue, along);
            }
        }

        return RasterImage.FromBytes(PngWriter.Rgb(width, height, pixels));
    }

    private static byte Channel(float from, float to, float along) => (byte)Math.Round((from + ((to - from) * along)) * 255);

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    private string Pick(string[] choices) => choices[_random.Next(choices.Length)];

    private int Between(int least, int most) => _random.Next(least, most + 1);

    private System.DateTime Day() => new System.DateTime(2000, 1, 1).AddDays(_random.Next(0, 30 * 365));

    /// <summary>An ink of a random hue at the saturation and lightness given, from HSL.</summary>
    private Ink Hue(float saturation, float lightness)
    {
        float hue = _random.Next(0, 360) / 60f;
        float chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        float second = chroma * (1 - Math.Abs((hue % 2) - 1));
        float lift = lightness - (chroma / 2);

        (float red, float green, float blue) = (int)hue switch
        {
            0 => (chroma, second, 0f),
            1 => (second, chroma, 0f),
            2 => (0f, chroma, second),
            3 => (0f, second, chroma),
            4 => (second, 0f, chroma),
            _ => (chroma, 0f, second),
        };

        return Pdf.Ink.Rgb(Byte(red + lift), Byte(green + lift), Byte(blue + lift));

        static byte Byte(float fraction) => (byte)Math.Round(Math.Min(1f, Math.Max(0f, fraction)) * 255);
    }
}
