using CsCheck;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.UnitTests.Fonts;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Font files are input from outside — users' fonts, whatever a machine has installed — so parsing them is an attack
/// surface. Real fonts, damaged at random, must either work or be rejected with <see cref="FontFormatException"/>:
/// never another exception, never a hang, never an allocation out of proportion to the file.
/// </summary>
/// <remarks>
/// Each damaged font is not only loaded but used: every table read, text measured, glyphs bounded and kerned, and a
/// subset built and read back. A defect reachable only through a lazily parsed table would otherwise go unseen.
/// </remarks>
public class FontFuzzTests
{
    private const long Iterations = 4000;

    /// <summary>Far longer than any real use of a font takes, so exceeding it means a loop that does not end.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private const string Text = "Hello, World! AVATAR To P. \u00C5\u00E9\u10D0\u10D5\uE000\uE003\uF041\U0001D400";

    private static readonly int[] Codepoints =
        [-1, 0, 0x20, 'A', 0xC5, 0x10D0, 0xE000, 0xF041, 0xFFFF, 0x1D400, 0x10FFFF];

    private static readonly string[] SeedFiles =
    [
        "NotoSansGeorgian-Regular.ttf", "SpecimenSans.ttc", "SpecimenCff-Regular.otf", "NotoSans-Italic.ttf"
    ];

    private static readonly Lazy<(byte[] Font, IReadOnlyList<(int Offset, int Length)> Tables)[]> Seeds = new(() =>
        SeedFiles.Select(file =>
        {
            byte[] font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", file));
            return (font, TableRanges(font));
        }).ToArray());

    private static Gen<FontMutation> Mutation { get; } = Gen.Select(
        Gen.Frequency((1, Gen.Const(0)), (2, Gen.Const(1)), (7, Gen.Const(2))),
        Gen.Int[0, int.MaxValue],
        Gen.Int[0, 3],
        Gen.Frequency((1, Gen.Int[0, 9]), (1, Gen.Int)),
        (region, selector, kind, value) => new FontMutation(region, selector, kind, value));

    [Fact]
    public void DamagedFontsWorkOrAreRejected()
    {
        Gen.Select(Gen.Int[0, SeedFiles.Length - 1], Mutation.Array[1, 24]).Sample(
            input =>
            {
                (byte[] seed, IReadOnlyList<(int Offset, int Length)> tables) = Seeds.Value[input.Item1];
                byte[] font = (byte[])seed.Clone();

                foreach (FontMutation mutation in input.Item2)
                    mutation.ApplyTo(font, tables);

                Survive(() => Exercise(font), font.Length);
            },
            iter: Iterations);
    }

    [Fact]
    public void TruncatedFontsWorkOrAreRejected()
    {
        Gen.Select(Gen.Int[0, SeedFiles.Length - 1], Gen.Double[0, 1]).Sample(
            input =>
            {
                byte[] seed = Seeds.Value[input.Item1].Font;
                byte[] font = seed.Take((int)(seed.Length * input.Item2)).ToArray();

                Survive(() => Exercise(font), font.Length);
            },
            iter: Iterations / 3);
    }

    [Fact]
    public void DamagedFontFilesScanOrAreRejected()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        int counter = 0;

        Gen.Select(Gen.Int[0, SeedFiles.Length - 1], Mutation.Array[1, 24]).Sample(
            input =>
            {
                (byte[] seed, IReadOnlyList<(int Offset, int Length)> tables) = Seeds.Value[input.Item1];
                byte[] font = (byte[])seed.Clone();

                foreach (FontMutation mutation in input.Item2)
                    mutation.ApplyTo(font, tables);

                string path = folder.Write($"{Interlocked.Increment(ref counter)}.ttf", font);
                Survive(() => Scan(path), font.Length);
                File.Delete(path);
            },
            iter: Iterations / 5);
    }

    /// <summary>Runs the action, allowing only <see cref="FontFormatException"/>, in bounded time and memory.</summary>
    private static void Survive(Func<long> action, int inputLength)
    {
        // A thread of its own: the fuzzer's workers block waiting on it, and on a busy runner a pool thread may not
        // come free within the patience allowed, failing a font that takes milliseconds.
        Task<long> run = Task.Factory.StartNew(() =>
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            try
            {
                action();
            }
            catch (FontFormatException)
            {
                // The one acceptable failure.
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Assert.True(run.Wait(Patience), "Using the damaged font did not finish.");

        // Generous — subsetting and re-reading copy the font a few times over — but finite: a count trusted without
        // checking asks for gigabytes.
        long budget = (64L * inputLength) + (256L << 20);
        Assert.True(run.Result < budget, $"Using a {inputLength}-byte font allocated {run.Result} bytes.");
    }

    private static long Exercise(byte[] data)
    {
        foreach (OpenTypeFont font in OpenTypeFont.LoadAll(data))
        {
            try
            {
                Use(font);
            }
            catch (FontFormatException)
            {
                // One face may be damaged and the next intact; keep going.
            }
        }

        return 0;
    }

    private static void Use(OpenTypeFont font)
    {
        _ = font.Names.PostScriptName;
        _ = font.Os2?.WeightClass;
        _ = font.Post?.ItalicAngle;
        _ = font.Style;
        _ = font.LineMetrics;
        _ = font.Embedding.AllowsEmbedding;

        CharacterMap map = font.CharacterMap;

        foreach (int codepoint in Codepoints)
            map.GetGlyph(codepoint);

        _ = map.EnumerateMappings().Count();
        _ = font.MeasureWidth(Text, 12f, 0.5f);
        _ = font.CountFitting(Text, 12f, 60f, 0.25f);

        int sample = Math.Min(font.GlyphCount, 64);

        for (int index = 0; index < sample; index++)
        {
            ushort glyph = (ushort)(index * (font.GlyphCount / sample));
            _ = font.GetAdvance(glyph, 10f);
            _ = font.GetKerning(glyph, (ushort)((glyph + 1) % font.GlyphCount));
            _ = font.TryGetGlyphBounds(glyph, out _);
        }

        _ = font.Descriptor.Flags;

        if (font.Cff is CompactFontTable cff)
        {
            for (int glyph = 0; glyph < Math.Min(cff.GlyphCount, 32); glyph++)
                _ = cff.GetCid((ushort)glyph);
        }

        if (font.Glyphs is null)
            return;

        GlyphSubset glyphs = new GlyphSubset(font);

        foreach (char character in Text)
            glyphs.Add(font.GetGlyphId(character), character);

        for (int index = 0; index < sample; index++)
            glyphs.Add((ushort)(index * (font.GlyphCount / sample)));

        // A subset, once made, is this library's own output and must read back cleanly — no exception at all.
        TrueTypeSubset subset = glyphs.Build();
        OpenTypeFont reread = OpenTypeFont.Load(subset.FontData);
        Assert.Equal(subset.GlyphCount, reread.GlyphCount);
        _ = reread.MeasureWidth(Text, 12f);
        _ = reread.Glyphs!.GetGlyphData((ushort)(reread.GlyphCount - 1));
    }

    private static long Scan(string path)
    {
        foreach (FontFaceInfo face in FontFileScanner.Scan(path))
        {
            _ = face.Names.FamilyAliases.Count;
            _ = FontFileScanner.ReadCharacterMap(path, face.FaceIndex).GetGlyph('A');
            _ = face.Covers('A');
        }

        FontCatalog catalog = FontCatalog.WithoutSystemFonts();
        catalog.RegisterFile(path);
        return 0;
    }

    /// <summary>The tables of every face, read from an intact seed font to aim mutations at.</summary>
    private static IReadOnlyList<(int Offset, int Length)> TableRanges(byte[] font)
    {
        List<(int Offset, int Length)> ranges = [];

        foreach (OpenTypeFont face in OpenTypeFont.LoadAll(font))
        {
            foreach (TableRecord record in face.Tables.Records)
                ranges.Add((record.Offset, record.Length));
        }

        return ranges;
    }
}
