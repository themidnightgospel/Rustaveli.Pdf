using System.Text;
using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Hinting taken out of the glyphs a document embeds — instructions gone, outlines untouched — and kept in the fonts
/// that cannot be read without it.
/// </summary>
public class GlyphHintingTests
{
    private static readonly string[] UnhintedTables = ["cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "post"];

    /// <summary>A simple glyph: one contour ending at point 3, three instruction bytes, then its points.</summary>
    private static readonly byte[] SimpleGlyph =
    [
        0, 1, 0, 0, 0, 0, 0, 10, 0, 10,
        0, 3,
        0, 3, 0xB0, 0x01, 0x2F,
        0x01, 0x01, 0x01, 0x01, 5, 5, 5, 5,
    ];

    [Fact]
    public void ASimpleGlyphLosesItsInstructionsAndKeepsEverythingElse()
    {
        byte[] stripped = GlyphHinting.Strip(SimpleGlyph);

        Assert.Equal(
            [0, 1, 0, 0, 0, 0, 0, 10, 0, 10, 0, 3, 0, 0, 0x01, 0x01, 0x01, 0x01, 5, 5, 5, 5],
            stripped);
    }

    [Fact]
    public void ASimpleGlyphWithoutInstructionsIsUnchanged()
    {
        byte[] plain = [0, 1, 0, 0, 0, 0, 0, 10, 0, 10, 0, 3, 0, 0, 0x01, 5];

        Assert.Equal(plain, GlyphHinting.Strip(plain));
    }

    [Fact]
    public void ACompositeGlyphLosesTheInstructionsAfterItsLastRecordAndTheFlagThatAnnouncesThem()
    {
        // Two records: the first with byte arguments and more to come, the last with word arguments and
        // instructions, which follow it — two bytes of them.
        byte[] composite =
        [
            0xFF, 0xFF, 0, 0, 0, 0, 0, 10, 0, 10,
            0x00, 0x20, 0, 7, 1, 2,
            0x01, 0x01, 0, 8, 0, 3, 0, 4,
            0, 2, 0xB0, 0x01,
        ];

        byte[] stripped = GlyphHinting.Strip(composite);

        Assert.Equal(
            [0xFF, 0xFF, 0, 0, 0, 0, 0, 10, 0, 10, 0x00, 0x20, 0, 7, 1, 2, 0x00, 0x01, 0, 8, 0, 3, 0, 4],
            stripped);
    }

    [Fact]
    public void ACompositeGlyphWithoutInstructionsKeepsItsRecords()
    {
        byte[] composite = [0xFF, 0xFF, 0, 0, 0, 0, 0, 10, 0, 10, 0x00, 0x00, 0, 7, 1, 2];

        Assert.Equal(composite, GlyphHinting.Strip(composite));
    }

    [Theory]
    [InlineData(new byte[] { 0, 1, 0, 0, 0, 0, 0, 10, 0 })]
    [InlineData(new byte[] { 0, 1, 0, 0, 0, 0, 0, 10, 0, 10, 0, 3, 0 })]
    [InlineData(new byte[] { 0, 1, 0, 0, 0, 0, 0, 10, 0, 10, 0, 3, 0, 9, 1 })]
    public void AGlyphCutShortIsRefused(byte[] glyph) =>
        Assert.Throws<FontFormatException>(() => GlyphHinting.Strip(glyph));

    [Fact]
    public void ByDefaultASubsetCarriesNoHintingAndTheSameOutlines()
    {
        OpenTypeFont font = TestFonts.Regular;
        ushort[] asked = "Hello, ÅåÉéñ".Select(character => font.GetGlyphId(character)).ToArray();

        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, asked);
        OpenTypeFont reparsed = OpenTypeFont.Load(subset.FontData);

        Assert.Equal(UnhintedTables, reparsed.Tables.Records.Select(record => TableTag.ToString(record.Tag)));

        for (ushort glyph = 0; glyph < reparsed.GlyphCount; glyph++)
        {
            ReadOnlySpan<byte> kept = reparsed.Glyphs!.GetGlyphData(glyph).Span;
            ReadOnlySpan<byte> original = font.Glyphs!.GetGlyphData(subset.OriginalGlyphIds[glyph]).Span;

            if (kept.IsEmpty)
                continue;

            Assert.True(font.Glyphs.TryGetBounds(subset.OriginalGlyphIds[glyph], out GlyphBounds before));
            Assert.True(reparsed.Glyphs.TryGetBounds(glyph, out GlyphBounds after));
            Assert.Equal(before, after);

            if (GlyphTable.IsComposite(kept))
            {
                CompositeGlyph.RecordsEnd(kept, out int lastFlagsAt);
                Assert.Equal(0, BigEndian.UInt16(kept, lastFlagsAt) & CompositeGlyph.HasInstructions);
                continue;
            }

            // The instructions are gone and every byte of the outline is the original's.
            int lengthAt = CompositeGlyph.HeaderSize + (2 * BigEndian.Int16(kept, 0));
            int instructions = BigEndian.UInt16(original, lengthAt);
            Assert.Equal(0, BigEndian.UInt16(kept, lengthAt));
            Assert.Equal(original.Slice(0, lengthAt).ToArray(), kept.Slice(0, lengthAt).ToArray());
            Assert.Equal(
                original.Slice(lengthAt + 2 + instructions).ToArray(),
                kept.Slice(lengthAt + 2).ToArray().AsSpan(0, original.Length - lengthAt - 2 - instructions).ToArray());
        }

        Assert.Contains(asked, glyph => GlyphTable.IsComposite(font.Glyphs!.GetGlyphData(glyph).Span));
        Assert.True(subset.FontData.Length < TrueTypeSubsetter.Subset(font, asked, keepHinting: true).FontData.Length / 2);
    }

    [Theory]
    [InlineData("MingLiU", null, true)]
    [InlineData("PMingLiU", null, true)]
    [InlineData("Something", "DFKai-SB", true)]
    [InlineData("Noto Sans", null, false)]
    [InlineData("Noto Sans", "Noto Sans Display", false)]
    public void FontsWhoseGlyphsTheirInstructionsAssembleAreKnownByName(string family, string? typographicFamily, bool needed) =>
        Assert.Equal(needed, GlyphHinting.IsNeededBy(new FontNames(family, "Regular", family, null, typographicFamily, null, [], [])));

    [Fact]
    public void AFontThatNeedsItsHintingKeepsIt()
    {
        // Noto Sans under a name its instructions would be needed for: the same name length, so the font stays whole.
        byte[] data = File.ReadAllBytes(TestFonts.PathOf(TestFonts.RegularFile));
        Replace(data, Encoding.BigEndianUnicode.GetBytes("Noto Sans"), Encoding.BigEndianUnicode.GetBytes("MingLiU X"));
        OpenTypeFont renamed = OpenTypeFont.Load(data);

        TrueTypeSubset subset = TrueTypeSubsetter.Subset(renamed, [renamed.GetGlyphId('A')]);

        Assert.Contains("MingLiU", renamed.Names.Family, StringComparison.Ordinal);
        Assert.True(OpenTypeFont.Load(subset.FontData).TryGetTable(TableTag.Fpgm, out _));
    }

    private static void Replace(byte[] data, byte[] find, byte[] replacement)
    {
        int replaced = 0;

        for (int at = 0; at <= data.Length - find.Length; at++)
        {
            if (data.AsSpan(at, find.Length).SequenceEqual(find))
            {
                replacement.CopyTo(data, at);
                replaced++;
            }
        }

        Assert.True(replaced > 0);
    }
}
