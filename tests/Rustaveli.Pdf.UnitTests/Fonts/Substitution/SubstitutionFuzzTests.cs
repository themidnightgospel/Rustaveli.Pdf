using System.Runtime.ExceptionServices;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>
/// A font's GSUB table is a program the font supplies, run over every piece of text set in it. Fonts whose GSUB and
/// GDEF tables are damaged at random must shape text or be rejected with <see cref="FontFormatException"/>: never
/// another exception, never a hang, never a glyph the font does not have, never clusters out of order.
/// </summary>
/// <remarks>
/// The damage is aimed at the two tables, and written as the values that break parsers — 0, 0xFFFF, 0x7FFFFFFF over
/// counts and offsets — as often as at random. The seeds are real fonts and a synthetic one carrying every lookup
/// type in every format, so every path meets damaged data. The random source is seeded, so a failure reproduces.
/// </remarks>
public class SubstitutionFuzzTests
{
    private const int Iterations = 2400;

    private const string Text =
        "office affluent ﬁ í ị́ ą ḿ 0︀ 1/2 l·l бг " +
        "ᾱ̓̀ ქართული aab cdabef yz stk xyc fi ffi " +
        "abcdefghijklmnopqrstuvwxyz";

    /// <summary>Far longer than shaping any text takes, so exceeding it means a loop that does not end.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly uint[] EdgeValues =
        [0, 1, 0x7F, 0x80, 0xFF, 0x7FFF, 0x8000, 0xFFFF, 0x7FFFFFFF, 0xFFFFFFFF];

    private static readonly (ScriptTag Script, LanguageTag Language)[] Systems =
    [
        (ScriptTag.Latin, LanguageTag.Default),
        (ScriptTag.Latin, LanguageTag.Parse("CAT ")),
        (ScriptTag.Cyrillic, LanguageTag.Parse("SRB ")),
        (ScriptTag.Greek, LanguageTag.Default),
        (ScriptTag.Arabic, LanguageTag.Default)
    ];

    private static readonly Lazy<Seed[]> Seeds = new(() =>
    [
        Seed.From(TestFonts.Bytes(TestFonts.RegularFile)),
        Seed.From(TestFonts.Bytes(TestFonts.LayoutFile)),
        Seed.From(TestFonts.Bytes(TestFonts.GeorgianFile)),
        Seed.From(Synthetic())
    ]);

    [Fact]
    public void DamagedSubstitutionsShapeOrAreRejected()
    {
        Random random = new Random(20260926);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Seed seed = Seeds.Value[iteration % Seeds.Value.Length];
            byte[] font = (byte[])seed.Font.Clone();
            int mutations = random.Next(1, 9);

            for (int mutation = 0; mutation < mutations; mutation++)
                Mutate(font, seed, random);

            FeatureSetting[] settings = seed.Features
                .Select(tag => new FeatureSetting(tag, random.Next(1, 4)))
                .Concat(GlyphSubstitutionTable.DefaultFeatures)
                .ToArray();

            Survive(() => Exercise(font, settings));
        }
    }

    [Fact]
    public void IntactSeedsShape()
    {
        foreach (Seed seed in Seeds.Value)
        {
            FeatureSetting[] settings = seed.Features.Select(FeatureSetting.On).ToArray();

            Assert.True(Exercise(seed.Font, settings) > 0);
        }
    }

    /// <summary>Shapes the text in every language system: how many shapings changed it, or -1 if rejected.</summary>
    private static int Exercise(byte[] data, FeatureSetting[] settings)
    {
        try
        {
            OpenTypeFont font = OpenTypeFont.Load(data);
            GlyphSubstitutionTable? table = font.Substitutions;
            int changed = 0;

            foreach ((ScriptTag script, LanguageTag language) in Systems)
            {
                GlyphBuffer original = GlyphBuffer.FromText(font, Text);
                GlyphBuffer buffer = GlyphBuffer.FromText(font, Text);
                table?.Apply(buffer, script, language, settings);
                Check(font, buffer);
                changed += buffer.Glyphs.SequenceEqual(original.Glyphs) ? 0 : 1;
            }

            return changed;
        }
        catch (FontFormatException)
        {
            return -1;
        }
    }

    private static void Check(OpenTypeFont font, GlyphBuffer buffer)
    {
        int previous = 0;

        for (int index = 0; index < buffer.Count; index++)
        {
            Assert.InRange(buffer.Glyphs[index], 0, font.GlyphCount - 1);
            Assert.InRange(buffer.Clusters[index], previous, Text.Length - 1);
            previous = buffer.Clusters[index];
        }
    }

    /// <summary>
    /// Runs the action on a thread of its own, so the time it is given is its own: queued to the thread pool, it could
    /// wait out the patience behind other tests' work on a busy machine before it ever started.
    /// </summary>
    private static void Survive(Action action)
    {
        Exception? failure = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        Assert.True(thread.Join(Patience), "Shaping with the damaged font did not finish.");

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Mutate(byte[] font, Seed seed, Random random)
    {
        (int start, int length) = random.Next(5) == 0 ? seed.Gdef : seed.Gsub;
        int position = start + random.Next(length);
        int kind = random.Next(4);
        uint value = random.Next(2) == 0 ? EdgeValues[random.Next(EdgeValues.Length)] : (uint)random.Next();
        int end = start + length;

        switch (kind)
        {
            case 0:
                font[position] = (byte)value;
                break;
            case 1:
                font[position] ^= (byte)(1 << random.Next(8));
                break;
            case 2 when position + 2 <= end:
                BigEndian.WriteUInt16(font, position, (ushort)value);
                break;
            case 3 when position + 4 <= end:
                BigEndian.WriteUInt32(font, position, value);
                break;
        }
    }

    /// <summary>
    /// A font of 40 glyphs — a to z as glyphs 1 to 26, combining acute and dot below as 27 and 28 — whose GSUB
    /// holds every lookup type in every format, nested lookups and lookup flags included, and whose GDEF has glyph
    /// classes, mark attachment classes and mark glyph sets.
    /// </summary>
    private static byte[] Synthetic()
    {
        byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);
        byte[] Letters() => SyntheticLayout.CoverageFormat2((1, 26, 0));
        byte[] Classes() => SyntheticLayout.ClassFormat2((1, 5, 1), (6, 26, 2));

        byte[] gsub = SyntheticSubstitution.Gsub(
            [("DFLT", (-1, [0]), []), ("latn", (-1, [0]), [("CAT ", (0, []))])],
            [("test", Enumerable.Range(0, 16).ToArray())],
            [
                Lookup(1, SingleFormat1(Cover(1), 1)),
                Lookup(1, SingleFormat2(Cover(2, 3), 29, 30)),
                Lookup(2, Multiple(Cover(4, 5), [4, 31], [])),
                Lookup(3, Alternate(Cover(6), [32, 33, 34])),
                Lookup(4, 0x0008, 0, Ligature(Cover(6, 9), [(35, [9]), (36, [6])], [(37, [9, 9])])),
                Lookup(5, ContextFormat1(Cover(15), [Rule([9], (0, 0), (1, 2))])),
                Lookup(5, ContextFormat2(
                    Letters(), Classes(), null, [Rule([2], (1, 1))], [Rule([1, 2], (0, 2), (2, 0))])),
                Lookup(5, ContextFormat3([Cover(20), Letters()], (1, 4), (0, 2))),
                Lookup(6, 0x0010, 1, ChainFormat1(Cover(3), [ChainRule([15], [6], [9], (0, 1), (1, 3))])),
                Lookup(6, 0x0100, 0, ChainFormat2(
                    Letters(), Classes(), Classes(), Classes(), null, [ChainRule([2], [2], [1], (0, 6))])),
                Lookup(6, ChainFormat3([Letters()], [Cover(24)], [Cover(25), Letters()], (0, 5), (0, 9))),
                Lookup(7, Extension(4, Ligature(Cover(19), [(38, [20])]))),
                Lookup(7, Extension(6, ChainFormat3([], [Cover(11)], [Cover(27)], (0, 0)))),
                Lookup(8, ReverseChain(Cover(1, 2), [Cover(3)], [Cover(4)], 39, 1)),
                Lookup(5, 0x0002, 0, ContextFormat3([Cover(27), Cover(28)], (0, 13))),
                Lookup(1, 0x0004, 0, SingleFormat1(SyntheticLayout.CoverageFormat2((29, 39, 0)), -28))
            ]);

        byte[] gdef = SyntheticLayout.Gdef(
            SyntheticLayout.ClassFormat2((1, 26, 1), (27, 28, 3), (35, 38, 2)),
            SyntheticLayout.ClassFormat1(27, 1, 2),
            [Cover(27), Cover(28)]);

        (int Code, int Glyph)[] characters = Enumerable.Range(0, 26)
            .Select(letter => ('a' + letter, letter + 1))
            .Concat([(0x301, 27), (0x323, 28)])
            .ToArray();

        return new SyntheticFont()
            .With("head", SyntheticTables.Head())
            .With("hhea", SyntheticTables.Hhea(numberOfHMetrics: 1))
            .With("maxp", SyntheticTables.Maxp(40, trueType: false))
            .With("hmtx", SyntheticTables.Hmtx([(500, 0)], new int[39]))
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(characters))))
            .With("GDEF", gdef)
            .With("GSUB", gsub)
            .Build();
    }

    /// <summary>A font to damage: its bytes, where its GSUB and GDEF lie, and the features its GSUB lists.</summary>
    private sealed record Seed(
        byte[] Font, (int Offset, int Length) Gsub, (int Offset, int Length) Gdef, FeatureTag[] Features)
    {
        /// <summary>
        /// The font cut down to the tables substitution reads — so each damaged copy is small — with the places of
        /// its GSUB and GDEF.
        /// </summary>
        public static Seed From(byte[] file)
        {
            OpenTypeFont original = OpenTypeFont.Load(file);
            SyntheticFont font = new SyntheticFont();

            foreach (string tag in new[] { "head", "hhea", "maxp", "hmtx", "cmap", "GSUB", "GDEF" })
            {
                Assert.True(original.TryGetTable(TableTag.FromString(tag), out ReadOnlyMemory<byte> table));
                font.With(tag, table.ToArray());
            }

            byte[] data = font.Build();
            OpenTypeFont cut = OpenTypeFont.Load(data);
            Assert.True(cut.Tables.TryGet(TableTag.Gsub, out TableRecord gsub));
            Assert.True(cut.Tables.TryGet(TableTag.Gdef, out TableRecord gdef));

            return new Seed(
                data, (gsub.Offset, gsub.Length), (gdef.Offset, gdef.Length), FeaturesOf(data, gsub.Offset));
        }

        private static FeatureTag[] FeaturesOf(byte[] data, int gsub)
        {
            int list = gsub + BigEndian.UInt16(data, gsub + 6);
            int count = BigEndian.UInt16(data, list);

            return Enumerable.Range(0, count)
                .Select(index => new FeatureTag(BigEndian.UInt32(data, list + 2 + (index * 6))))
                .Distinct()
                .ToArray();
        }
    }
}
