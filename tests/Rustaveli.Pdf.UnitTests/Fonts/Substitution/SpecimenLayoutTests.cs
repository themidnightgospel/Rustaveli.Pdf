using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>
/// Specimen Layout, whose GSUB and GDEF fontTools compiled from the feature file in tests/assets/fonts/derive.py,
/// shaped one feature at a time. The expected glyphs and clusters are HarfBuzz's for the same font and text, with
/// clusters kept per character — an independent shaper's reading of the same specification.
/// </summary>
public class SpecimenLayoutTests
{
    public static TheoryData<string, int, string, int[], int[]> Cases => new()
    {
        // Ligatures, longest first, formed past marks, which stay after them in their cluster.
        { "ss01", 1, "ffi fi ff fl", [100, 1, 98, 1, 97, 1, 99], [0, 3, 4, 6, 7, 9, 10] },
        { "ss01", 1, "f́i", [98, 103], [0, 0] },
        { "ss01", 1, "ff̣i", [100, 104], [0, 0] },

        // Glyph contexts, with two glyphs before and after, and lookups at two input glyphs.
        { "ss02", 1, "cdabef", [68, 69, 34, 35, 70, 71], [0, 1, 2, 3, 4, 5] },
        { "ss02", 1, "cdabe", [68, 69, 66, 67, 70], [0, 1, 2, 3, 4] },
        { "ss02", 1, "dabef", [69, 66, 67, 70, 71], [0, 1, 2, 3, 4] },
        { "ss02", 1, "cdaxbef", [68, 69, 34, 89, 67, 70, 71], [0, 1, 2, 3, 4, 5, 6] },

        // Class contexts in several subtables, the first that applies winning.
        { "ss03", 1, "banana", [67, 34, 79, 34, 79, 66], [0, 1, 2, 3, 4, 5] },
        { "ss03", 1, "aeiou", [66, 38, 74, 48, 86], [0, 1, 2, 3, 4] },
        { "ss03", 1, "strength", [84, 53, 83, 38, 79, 72, 85, 73], [0, 1, 2, 3, 4, 5, 6, 7] },
        { "ss03", 1, "beauty", [35, 70, 34, 86, 85, 90], [0, 1, 2, 3, 4, 5] },

        // A nested multiple substitution: the next record's index counts the glyph it added.
        { "ss04", 1, "yz", [86, 55, 91], [0, 0, 1] },
        { "ss04", 1, "xyzy", [89, 86, 55, 91, 90], [0, 1, 1, 2, 3] },

        // A nested ligature: a record indexing past the shortened input does nothing.
        { "ss05", 1, "stk", [97, 76], [0, 2] },
        { "ss05", 1, "st", [97], [0] },
        { "ss05", 1, "stks", [97, 76, 84], [0, 2, 3] },

        // Reverse chaining.
        { "ss06", 1, "aab", [67, 67, 67], [0, 1, 2] },
        { "ss06", 1, "aabxyc", [67, 67, 67, 89, 90, 36], [0, 1, 2, 3, 4, 5] },
        { "ss06", 1, "abxc", [67, 67, 89, 68], [0, 1, 2, 3] },

        // A mark filtering set.
        { "ss07", 1, "í", [101, 103], [0, 1] },
        { "ss07", 1, "ị́", [101, 104, 103], [0, 1, 2] },
        { "ss07", 1, "ị", [74, 104], [0, 1] },

        // A mark attachment type.
        { "ss08", 1, "x̣́", [57, 104, 103], [0, 1, 2] },
        { "ss08", 1, "x̀́", [89, 102, 103], [0, 1, 2] },
        { "ss08", 1, "x́", [57, 103], [0, 1] },

        // Extension lookups.
        { "ss09", 1, "fi ab", [98, 1, 34, 67], [0, 2, 3, 4] },

        // Ligatures passed over.
        { "ss10", 1, "xﬁy", [57, 98, 90], [0, 1, 2] },
        { "ss10", 1, "xfy", [89, 71, 90], [0, 1, 2] },

        // Alternates chosen by value.
        { "salt", 1, "a", [34], [0] },
        { "salt", 2, "a", [35], [0] },
        { "salt", 3, "a", [36], [0] },
        { "salt", 4, "a", [66], [0] }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ShapesAsAnIndependentShaperDoes(string feature, int value, string text, int[] glyphs, int[] clusters)
    {
        OpenTypeFont font = TestFonts.Layout;
        GlyphBuffer buffer = GlyphBuffer.FromText(font, text);

        font.Substitutions!.Apply(
            buffer, ScriptTag.Latin, LanguageTag.Default, [new FeatureSetting(FeatureTag.Parse(feature), value)]);

        Assert.Equal(glyphs, Glyphs(buffer));
        Assert.Equal(clusters, Clusters(buffer));
    }

    [Fact]
    public void AppliesNoFeatureTheSettingsLeaveOff()
    {
        OpenTypeFont font = TestFonts.Layout;
        GlyphBuffer buffer = GlyphBuffer.FromText(font, "ffi cdabef aab");

        font.Substitutions!.Apply(buffer, ScriptTag.Latin, LanguageTag.Default, GlyphSubstitutionTable.DefaultFeatures);

        Assert.Equal(GlyphBuffer.FromText(font, "ffi cdabef aab").Glyphs.ToArray(), buffer.Glyphs.ToArray());
    }
}
