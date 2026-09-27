using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>The limits a session sets on the work and growth a font's lookups may cause.</summary>
public class SubstitutionSessionTests
{
    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    [Fact]
    public void AllowsWorkInProportionToTheText()
    {
        // Every glyph costs a unit per subtable tried: 10 001 here, of which only the last applies. Twenty glyphs
        // cost 200 020 units, well within what twenty glyphs are allowed, but far beyond a fixed allowance.
        FontBytes lookup = new FontBytes().U16(1).U16(0).U16(10001);
        int misses = 6 + (2 * 10001);

        for (int subtable = 0; subtable < 10000; subtable++)
            lookup.U16(misses);

        lookup.U16(misses + 12).Bytes(SingleFormat1(Cover(999), 1)).Bytes(SingleFormat1(Cover(1), 1));
        byte[] gsub = SyntheticSubstitution.Gsub([lookup.ToArray()]);

        GlyphBuffer buffer = Apply(gsub, Enumerable.Repeat(1, 20).ToArray());

        Assert.Equal(Enumerable.Repeat(2, 20).ToArray(), Glyphs(buffer));
    }

    [Fact]
    public void AllowsGrowthInProportionToTheText()
    {
        // A hundred glyphs may grow to 256 + 16 × 100 = 1 856. Each substitution adds nineteen, so 92 are made —
        // reaching 1 848 — and the other eight glyphs are left as they were.
        byte[] gsub = OneLookup(2, Multiple(Cover(1), Enumerable.Repeat(2, 20).ToArray()));

        GlyphBuffer buffer = Apply(gsub, Enumerable.Repeat(1, 100).ToArray());

        Assert.Equal(1848, buffer.Count);
        Assert.Equal(Enumerable.Repeat(1, 8).ToArray(), Glyphs(buffer).Skip(1840).ToArray());
        Assert.All(Glyphs(buffer).Take(1840), glyph => Assert.Equal(2, glyph));
    }

    [Fact]
    public void KeepsOnlyTheEditsMadeAtTheCurrentPosition()
    {
        GlyphBuffer buffer = Buffer(1, 1);
        GlyphSubstitutionTable table = Table(OneLookup(2, Multiple(Cover(1), [1, 2])));
        SubstitutionSession session = new SubstitutionSession(table, buffer);

        table.Apply(session, [(0, 1)]);

        Assert.Equal(new[] { 1, 2, 1, 2 }, Glyphs(buffer));
        Assert.Equal(1, session.EditCount);
    }

    [Fact]
    public void GivesEachNestingDepthItsOwnPositions()
    {
        GlyphSubstitutionTable table = Table(SyntheticSubstitution.Gsub([Lookup(1, SingleFormat1(Cover(1), 1))]));
        SubstitutionSession session = new SubstitutionSession(table, Buffer(1));
        List<int> outer = session.Positions();

        Assert.True(session.TryEnter());
        List<int> inner = session.Positions();
        session.Leave();

        Assert.NotSame(outer, inner);
        Assert.Same(outer, session.Positions());
        Assert.Equal(0, session.Depth);
    }
}
