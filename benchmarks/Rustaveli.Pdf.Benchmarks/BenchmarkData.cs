using SkiaSharp;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// Deterministic content shared by both libraries' documents. Seeded, so every run measures exactly the same work.
/// </summary>
public static class BenchmarkData
{
    private static readonly string[] Words =
    [
        "layout", "measure", "column", "margin", "gutter", "leading", "paragraph", "invoice", "total", "amount",
        "quarterly", "statement", "account", "balance", "period", "delivery", "shipment", "reference", "customer",
        "order", "quantity", "price", "discount", "carrier", "warehouse", "a", "the", "of", "and", "to", "in", "for"
    ];

    public static IReadOnlyList<string> ReportParagraphs { get; } = Paragraphs(count: 900, seed: 1);

    public static IReadOnlyList<LineItem> InvoiceLines { get; } = Lines(count: 20, seed: 2);

    public static IReadOnlyList<LineItem> TableRows { get; } = Lines(count: 10_000, seed: 3);

    /// <summary>Four distinct 800×600 PNG images, generated rather than committed.</summary>
    public static IReadOnlyList<byte[]> Photographs { get; } = Enumerable.Range(0, 4).Select(Photograph).ToList();

    private static List<string> Paragraphs(int count, int seed)
    {
        Random random = new Random(seed);
        List<string> paragraphs = new List<string>(count);

        for (int index = 0; index < count; index++)
        {
            int length = random.Next(30, 90);
            paragraphs.Add(string.Join(" ", Enumerable.Range(0, length).Select(_ => Words[random.Next(Words.Length)])) + ".");
        }

        return paragraphs;
    }

    private static List<LineItem> Lines(int count, int seed)
    {
        Random random = new Random(seed);
        List<LineItem> lines = new List<LineItem>(count);

        for (int index = 0; index < count; index++)
        {
            string description = string.Join(" ", Enumerable.Range(0, random.Next(2, 7)).Select(_ => Words[random.Next(Words.Length)]));
            decimal amount = random.Next(100, 999_999) / 100m;
            lines.Add(new LineItem($"SKU-{index:D5}", description, amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        return lines;
    }

    private static byte[] Photograph(int index)
    {
        using SKBitmap bitmap = new SKBitmap(800, 600);
        using SKCanvas canvas = new SKCanvas(bitmap);
        using SKPaint paint = new SKPaint();

        // A gradient plus noise-like stripes: compressible enough to be realistic, varied enough not to be trivial.
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(800, 600),
            [new SKColor((byte)(40 * index), 90, 160), new SKColor(220, (byte)(50 * index), 80)],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, 800, 600, paint);

        paint.Shader = null;
        Random random = new Random(index);
        for (int stripe = 0; stripe < 200; stripe++)
        {
            paint.Color = new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 60);
            canvas.DrawRect(random.Next(800), random.Next(600), random.Next(10, 120), random.Next(2, 12), paint);
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
