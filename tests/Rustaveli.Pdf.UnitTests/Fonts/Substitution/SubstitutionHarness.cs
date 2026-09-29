using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>Applies synthetic GSUB tables to runs of glyphs, each glyph starting in a cluster of its own.</summary>
internal static class SubstitutionHarness
{
    /// <summary>More glyphs than synthetic tables name, so no substitution is refused for a missing glyph.</summary>
    public const int GlyphCount = 1000;

    /// <summary>The feature <see cref="SyntheticSubstitution.Gsub(byte[][], int[])"/> lists.</summary>
    public static readonly FeatureTag Feature = FeatureTag.Parse("test");

    public static GlyphBuffer Buffer(params int[] glyphs)
    {
        GlyphBuffer buffer = new GlyphBuffer();

        for (int index = 0; index < glyphs.Length; index++)
            buffer.Add((ushort)glyphs[index], index);

        return buffer;
    }

    public static GlyphSubstitutionTable Table(byte[] gsub, byte[]? gdef = null, int glyphCount = GlyphCount) =>
        new GlyphSubstitutionTable(gsub, gdef is null ? null : new GlyphDefinitionTable(gdef), glyphCount);

    /// <summary>Applies the table's "test" feature, with <paramref name="value"/>, to the glyphs.</summary>
    public static GlyphBuffer Apply(byte[] gsub, int[] glyphs, byte[]? gdef = null, int value = 1)
    {
        GlyphBuffer buffer = Buffer(glyphs);
        Table(gsub, gdef).Apply(buffer, ScriptTag.Default, LanguageTag.Default, [new FeatureSetting(Feature, value)]);
        return buffer;
    }

    /// <summary>Applies lookup 0 of the table to the glyphs through a session with the given limits.</summary>
    public static GlyphBuffer ApplyWithLimits(
        byte[] gsub, int[] glyphs, long work, int maximumLength, byte[]? gdef = null)
    {
        GlyphBuffer buffer = Buffer(glyphs);
        GlyphSubstitutionTable table = Table(gsub, gdef);
        table.Apply(new SubstitutionSession(table, buffer, work, maximumLength), [(0, 1)]);
        return buffer;
    }

    public static int[] Glyphs(GlyphBuffer buffer) => buffer.Glyphs.ToArray().Select(glyph => (int)glyph).ToArray();

    public static int[] Clusters(GlyphBuffer buffer) => buffer.Clusters.ToArray();

    /// <summary>
    /// Reads a subtable of lookup type <paramref name="type"/> on its own. A table leaves out a subtable it cannot
    /// read, so the error a damaged one raises is seen only here.
    /// </summary>
    public static SubstitutionSubtable ReadSubtable(int type, byte[] subtable) =>
        SubstitutionSubtable.Read(subtable, type, 0);

    /// <summary>A one-lookup table of the given type, the lookup being the feature's.</summary>
    public static byte[] OneLookup(int type, params byte[][] subtables) =>
        SyntheticSubstitution.Gsub([SyntheticSubstitution.Lookup(type, subtables)]);
}
