using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class GlyphBufferTests
{
    [Fact]
    public void MapsEachCharacterToItsGlyphInAClusterAtItsOffset()
    {
        OpenTypeFont font = SyntheticFont.Minimal().Load();

        GlyphBuffer buffer = GlyphBuffer.FromText(font, "AB\U0001D400A");

        Assert.Equal(new[] { 1, 2, 0, 1 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 2, 4 }, Clusters(buffer));
    }

    [Fact]
    public void MapsALoneSurrogateAsAMissingCharacter()
    {
        // Built in code: test data holding a lone surrogate does not survive being passed to a theory intact.
        OpenTypeFont font = SyntheticFont.Minimal().Load();
        (string Text, int[] Glyphs)[] cases =
        [
            ("A" + '\uD800', [1, 0]),
            ('\uD800' + "A", [0, 1]),
            (new string(['\uDC00', '\uD800']), [0, 0]),
            ("", [])
        ];

        foreach ((string text, int[] glyphs) in cases)
        {
            GlyphBuffer buffer = GlyphBuffer.FromText(font, text);

            Assert.Equal(glyphs, Glyphs(buffer));
            Assert.Equal(Enumerable.Range(0, glyphs.Length).ToArray(), Clusters(buffer));
        }
    }

    [Fact]
    public void RejectsBuildingFromTextWithoutAFont()
    {
        Assert.Throws<ArgumentNullException>(() => GlyphBuffer.FromText(null!, "A"));
    }

    [Fact]
    public void GrowsPastItsCapacity()
    {
        GlyphBuffer empty = new GlyphBuffer(0);
        GlyphBuffer defaulted = new GlyphBuffer();

        for (int glyph = 1; glyph <= 40; glyph++)
        {
            empty.Add((ushort)glyph, glyph * 10);
            defaulted.Add((ushort)glyph, glyph * 10);
        }

        Assert.Equal(40, empty.Count);
        Assert.Equal(Enumerable.Range(1, 40).ToArray(), Glyphs(empty));
        Assert.Equal(Enumerable.Range(1, 40).Select(glyph => glyph * 10).ToArray(), Clusters(defaulted));
    }

    [Fact]
    public void DoublesItsCapacityWhenItGrows()
    {
        GlyphBuffer buffer = new GlyphBuffer(4);

        for (int glyph = 1; glyph <= 4; glyph++)
            buffer.Add((ushort)glyph, glyph);

        Assert.Equal(4, buffer.Capacity);

        buffer.Add(5, 5);

        Assert.Equal(8, buffer.Capacity);

        buffer.InsertAfter(0, 10);

        Assert.Equal(16, buffer.Capacity);

        // Twenty more is more than doubling makes room for, so the buffer grows to exactly what is asked.
        buffer.InsertAfter(0, 20);

        Assert.Equal(35, buffer.Capacity);
        Assert.Equal(35, buffer.Count);
    }

    [Fact]
    public void RejectsANegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlyphBuffer(-1));
    }

    [Fact]
    public void InsertsCopiesOfAGlyphInItsCluster()
    {
        GlyphBuffer buffer = Buffer(5, 6, 7);

        buffer.InsertAfter(1, 2);

        Assert.Equal(new[] { 5, 6, 6, 6, 7 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 1, 1, 2 }, Clusters(buffer));

        buffer.InsertAfter(4, 1);

        Assert.Equal(new[] { 5, 6, 6, 6, 7, 7 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 1, 1, 2, 2 }, Clusters(buffer));
    }

    [Fact]
    public void GrowsToInsertPastItsCapacity()
    {
        GlyphBuffer buffer = new GlyphBuffer(1);
        buffer.Add(9, 0);

        buffer.InsertAfter(0, 3);

        Assert.Equal(new[] { 9, 9, 9, 9 }, Glyphs(buffer));
    }

    [Fact]
    public void RemovesGlyphs()
    {
        GlyphBuffer buffer = Buffer(5, 6, 7, 8);

        buffer.RemoveAt(1);
        buffer.RemoveAt(2);

        Assert.Equal(new[] { 5, 7 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2 }, Clusters(buffer));
    }

    [Fact]
    public void ReplacesAGlyphKeepingItsCluster()
    {
        GlyphBuffer buffer = Buffer(5, 6);

        buffer.Replace(1, 60);

        Assert.Equal(new[] { 5, 60 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1 }, Clusters(buffer));
    }

    [Fact]
    public void MergesWholeClustersAtBothEnds()
    {
        GlyphBuffer buffer = new GlyphBuffer();

        foreach ((int glyph, int cluster) in new[] { (1, 0), (2, 1), (3, 1), (4, 2), (5, 2), (6, 3) })
            buffer.Add((ushort)glyph, cluster);

        // Merging the second glyph of cluster 1 with the first of cluster 2 takes in both clusters whole.
        buffer.MergeClusters(2, 3);

        Assert.Equal(new[] { 0, 1, 1, 1, 1, 3 }, Clusters(buffer));
    }

    [Fact]
    public void MergesToTheSmallestClusterWhateverTheOrder()
    {
        GlyphBuffer buffer = new GlyphBuffer();

        foreach (int cluster in new[] { 9, 7, 3, 5 })
            buffer.Add(1, cluster);

        buffer.MergeClusters(0, 2);

        Assert.Equal(new[] { 3, 3, 3, 5 }, Clusters(buffer));
    }

    [Fact]
    public void MergesTheWholeBuffer()
    {
        // Sized exactly, so nothing past the last glyph could be read unnoticed.
        GlyphBuffer buffer = new GlyphBuffer(3);

        for (int glyph = 0; glyph < 3; glyph++)
            buffer.Add((ushort)(glyph + 1), glyph);

        buffer.MergeClusters(0, 2);

        Assert.Equal(new[] { 0, 0, 0 }, Clusters(buffer));
    }

    [Fact]
    public void EndsAClusterAtTheNextClusterOrTheText()
    {
        GlyphBuffer buffer = new GlyphBuffer();

        foreach ((int glyph, int cluster) in new[] { (1, 0), (2, 1), (3, 1), (4, 4) })
            buffer.Add((ushort)glyph, cluster);

        Assert.Equal(1, buffer.GetClusterEnd(0, 6));
        Assert.Equal(4, buffer.GetClusterEnd(1, 6));
        Assert.Equal(4, buffer.GetClusterEnd(2, 6));
        Assert.Equal(6, buffer.GetClusterEnd(3, 6));
    }
}
