using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class AlternateSubstitutionTests
{
    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 21)]
    [InlineData(3, 22)]
    [InlineData(4, 2)]
    [InlineData(-1, 2)]
    public void ChoosesTheAlternateTheFeatureValueNames(int value, int expected)
    {
        GlyphBuffer buffer = Apply(OneLookup(3, Alternate(Cover(2), [20, 21, 22])), [1, 2], value: value);

        Assert.Equal(new[] { 1, expected }, Glyphs(buffer));
    }

    [Fact]
    public void ChoosesNoAlternateForAValueOfZero()
    {
        GlyphBuffer buffer = Buffer(2);
        GlyphSubstitutionTable table = Table(OneLookup(3, Alternate(Cover(2), [20])));

        table.Apply(new SubstitutionSession(table, buffer), [(0, 0)]);

        Assert.Equal(new[] { 2 }, Glyphs(buffer));
    }

    [Fact]
    public void IgnoresCoverageIndicesPastTheSets()
    {
        GlyphBuffer buffer = Apply(OneLookup(3, Alternate(Cover(2, 3), [20])), [2, 3]);

        Assert.Equal(new[] { 20, 3 }, Glyphs(buffer));
    }

    [Fact]
    public void RefusesAnAlternateTheFontLacks()
    {
        GlyphBuffer buffer = Apply(OneLookup(3, Alternate(Cover(2), [1000])), [2]);

        Assert.Equal(new[] { 2 }, Glyphs(buffer));
    }

    [Fact]
    public void RejectsSetsPastTheTable()
    {
        byte[] subtable = Alternate(Cover(2), [20]);
        BigEndian.WriteUInt16(subtable, 4, 5000);

        Assert.Throws<FontFormatException>(() => ReadSubtable(3, subtable));
    }
}
