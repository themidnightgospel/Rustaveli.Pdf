using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Type 2 charstrings with their subroutines written out in place: the same drawing, with no call left in it.
/// </summary>
public class CharStringFlattenerTests
{
    private const byte CallSubr = 10;
    private const byte Return = 11;
    private const byte EndChar = 14;
    private const byte HStem = 1;
    private const byte HintMask = 19;
    private const byte RLineTo = 5;
    private const byte CallGSubr = 29;

    /// <summary>An operand as a charstring writes a small integer: one byte, its value plus 139.</summary>
    private static byte N(int value) => (byte)(value + 139);

    /// <summary>
    /// A CFF-like buffer holding the charstring, then a local and a global subroutine INDEX; the charstring is
    /// flattened against them.
    /// </summary>
    private static byte[] Flatten(byte[] charString, byte[][]? local = null, byte[][]? global = null)
    {
        FontDataWriter data = new FontDataWriter();
        data.Bytes(charString);
        int localAt = data.Length;
        WriteIndex(data, local ?? []);
        int globalAt = data.Length;
        WriteIndex(data, global ?? []);
        byte[] cff = data.ToArray();

        return CharStringFlattener.Flatten(cff, 0, charString.Length, CffIndex.Read(cff, globalAt), CffIndex.Read(cff, localAt));
    }

    private static void WriteIndex(FontDataWriter data, byte[][] items)
    {
        data.UInt16(items.Length);

        if (items.Length == 0)
            return;

        data.UInt8(4);
        uint offset = 1;
        data.UInt32(offset);

        foreach (byte[] item in items)
        {
            offset += (uint)item.Length;
            data.UInt32(offset);
        }

        foreach (byte[] item in items)
            data.Bytes(item);
    }

    [Theory]
    [InlineData(0, 107)]
    [InlineData(1239, 107)]
    [InlineData(1240, 1131)]
    [InlineData(33899, 1131)]
    [InlineData(33900, 32768)]
    public void TheSubroutineBiasGrowsWithTheirNumber(int count, int bias) => Assert.Equal(bias, CharStringFlattener.Bias(count));

    [Fact]
    public void AGlyphWithoutCallsIsUnchanged()
    {
        byte[] glyph = [N(10), N(20), RLineTo, 28, 0x01, 0x00, N(0), RLineTo, 255, 0, 1, 0, 0, N(3), RLineTo, 247, 0, N(-5), RLineTo, EndChar];

        Assert.Equal(glyph, Flatten(glyph));
    }

    [Fact]
    public void LocalAndGlobalSubroutinesAreWrittenOutInPlace()
    {
        // Subroutine numbers are biased by 107 with fewer than 1240 of them: -107 is subroutine 0.
        byte[] glyph = [N(1), N(2), N(-107), CallSubr, N(-107), CallGSubr, EndChar];
        byte[][] local = [[N(3), RLineTo, Return]];
        byte[][] global = [[N(4), N(5), RLineTo, Return]];

        Assert.Equal([N(1), N(2), N(3), RLineTo, N(4), N(5), RLineTo, EndChar], Flatten(glyph, local, global));
    }

    [Fact]
    public void SubroutinesMayCallSubroutinesAndEndTheGlyph()
    {
        // The caller's bytes after a call that ends the glyph are never reached.
        byte[] glyph = [N(-107), CallSubr, N(9), RLineTo, EndChar];
        byte[][] local = [[N(1), N(-106), CallSubr, Return], [N(2), RLineTo, EndChar]];

        Assert.Equal([N(1), N(2), RLineTo, EndChar], Flatten(glyph, local));
    }

    [Fact]
    public void ASubroutineMayEndWhereItsBytesEnd()
    {
        byte[] glyph = [N(-107), CallSubr, RLineTo, EndChar];
        byte[][] local = [[N(1), N(2)]];

        Assert.Equal([N(1), N(2), RLineTo, EndChar], Flatten(glyph, local));
    }

    [Fact]
    public void StemsDeclaredAcrossACallAreCountedForTheHintMask()
    {
        // Five stems pushed before the call, four in it: nine stems, so the mask that follows is two bytes. Counting
        // only the operands still unwritten at the stem operator would find four and take one.
        byte[] stems = [N(0), N(1), N(2), N(3), N(4), N(5), N(6), N(7), N(8), N(9)];
        byte[] glyph = [.. stems, N(-107), CallSubr, HintMask, 0xFF, 0x80, N(1), N(1), RLineTo, EndChar];
        byte[][] local = [[N(10), N(11), N(12), N(13), N(14), N(15), N(16), N(17), HStem, Return]];

        Assert.Equal(
            [.. stems, N(10), N(11), N(12), N(13), N(14), N(15), N(16), N(17), HStem, HintMask, 0xFF, 0x80, N(1), N(1), RLineTo, EndChar],
            Flatten(glyph, local));
    }

    [Fact]
    public void OperandsBeforeAHintMaskAreStems()
    {
        // A width and eighteen operands: nine implicit vertical stems, so two mask bytes.
        byte[] operands = [N(50), .. Enumerable.Range(0, 18).Select(value => N(value))];
        byte[] glyph = [.. operands, HintMask, 0xAA, 0x80, N(1), N(1), RLineTo, EndChar];

        Assert.Equal(glyph, Flatten(glyph));
    }

    [Fact]
    public void FlexOperatorsAreKept()
    {
        byte[] glyph = [.. Enumerable.Range(0, 13).Select(value => N(value)), 12, 35, EndChar];

        Assert.Equal(glyph, Flatten(glyph));
    }

    [Fact]
    public void AWidthBeforeEndcharIsKept()
    {
        byte[] glyph = [N(40), EndChar];

        Assert.Equal(glyph, Flatten(glyph));
    }

    [Theory]
    [InlineData(new byte[] { 139 + 1, 139 + 2, 139 + 3, 139 + 4, EndChar })]
    [InlineData(new byte[] { 139 + 1, 139 + 2, 12, 10, EndChar })]
    [InlineData(new byte[] { 255, 0, 0, 0x80, 0, CallSubr, EndChar })]
    public void WhatCannotBeWrittenOutIsRefused(byte[] glyph) =>
        Assert.Throws<NotSupportedException>(() => Flatten(glyph, [[Return]]));

    [Fact]
    public void ANumberLeftByASubroutineCannotBeCalled()
    {
        byte[] glyph = [N(-107), CallSubr, CallSubr, EndChar];

        Assert.Throws<NotSupportedException>(() => Flatten(glyph, [[N(-107), Return]]));
    }

    [Fact]
    public void CallsNestedTooDeepAreRefused()
    {
        // Subroutine 0 calls itself for ever.
        Assert.Throws<NotSupportedException>(() => Flatten([N(-107), CallSubr, EndChar], [[N(-107), CallSubr, Return]]));
    }

    [Theory]
    [InlineData(new byte[] { CallSubr, EndChar })]
    [InlineData(new byte[] { 139 + 5, CallSubr, EndChar })]
    [InlineData(new byte[] { 139 + 1, 139 + 1, RLineTo })]
    [InlineData(new byte[] { 0, EndChar })]
    [InlineData(new byte[] { 28, 0 })]
    [InlineData(new byte[] { 139 + 1, 139 + 1, HStem, HintMask })]
    [InlineData(new byte[] { 12 })]
    public void AMalformedGlyphIsRefused(byte[] glyph) =>
        Assert.Throws<FontFormatException>(() => Flatten(glyph, [[Return]]));
}
