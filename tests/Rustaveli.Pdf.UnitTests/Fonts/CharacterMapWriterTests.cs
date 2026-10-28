using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class CharacterMapWriterTests
{
    [Fact]
    public void WritesEachSubtableAfterTheRecordsThatPointToIt()
    {
        // A and B map to consecutive glyphs, so they share a segment and a group; a and U+1D400 each start their own.
        KeyValuePair<int, ushort>[] mappings = [new('A', 1), new('B', 2), new('a', 5), new(0x1D400, 3)];

        byte[] expected = new FontBytes()
            .U16(0).U16(2)
            .U16(3).U16(1).U32(20)
            .U16(3).U16(10).U32(60)
            // Format 4: three segments, so a search range of 4, an entry selector of 1 and a range shift of 2.
            .U16(4).U16(40).U16(0).U16(6).U16(4).U16(1).U16(2)
            .U16('B').U16('a').U16(0xFFFF)
            .U16(0)
            .U16('A').U16('a').U16(0xFFFF)
            .U16((1 - 'A') & 0xFFFF).U16((5 - 'a') & 0xFFFF).U16(1)
            .U16(0).U16(0).U16(0)
            // Format 12: three groups.
            .U16(12).U16(0).U32(52).U32(0).U32(3)
            .U32('A').U32('B').U32(1)
            .U32('a').U32('a').U32(5)
            .U32(0x1D400).U32(0x1D400).U32(3)
            .ToArray();

        Assert.Equal(expected, CharacterMapWriter.Write(mappings, symbol: false));
    }

    [Fact]
    public void WritesNoSubtableForASymbolFontWhoseFormat4WouldOverflow()
    {
        // Every other code, each to the next glyph: one segment each would pass format 4's 64 KB.
        IEnumerable<KeyValuePair<int, ushort>> mappings = Enumerable.Range(1, 9000)
            .Select(glyph => new KeyValuePair<int, ushort>(2 * glyph, (ushort)glyph));

        Assert.Equal(new FontBytes().U16(0).U16(0).ToArray(), CharacterMapWriter.Write(mappings, symbol: true));
    }
}
