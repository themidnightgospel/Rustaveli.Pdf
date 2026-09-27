using Rustaveli.Pdf.Text.Bidi;
using static Rustaveli.Pdf.UnitTests.Bidi.BidiText;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>The paragraph as the layout engine uses it: plain text, surrogate pairs, lines and their arguments.</summary>
public class BidiParagraphTests
{
    [Theory]
    [InlineData("Hello, world: 1,234.50 \u20AC (100%) \u00BFQu\u00E9?\t\"quoted\"")]
    [InlineData("\u0395\u03BB\u03BB\u03B7\u03BD\u03B9\u03BA\u03AC \u041A\u0438\u0440\u0438\u043B\u043B\u0438\u0446\u0430 \u4E2D\u6587 \u0939\u093F\u0928\u094D\u0926\u0940")]
    [InlineData("emoji \U0001F600 and a musical symbol \U0001D11E")]
    [InlineData("lone surrogates \uD800 \uDC00 \uD800")]
    [InlineData("\u200Bzero width \u200D joiners and marks e\u0301")]
    public void SetsLeftToRightTextAsOneLeftToRightRun(string text)
    {
        BidiParagraph paragraph = new BidiParagraph(text.AsSpan());
        List<BidiRun> runs = new List<BidiRun>();
        byte[] levels = Enumerable.Repeat((byte)9, text.Length).ToArray();

        paragraph.GetVisualRuns(0, text.Length, runs);
        paragraph.GetLineLevels(0, text.Length, levels);

        Assert.Equal(text.Length, paragraph.Length);
        Assert.Equal(0, paragraph.ParagraphLevel);
        Assert.True(paragraph.IsLeftToRightOnly);
        Assert.Equal([new BidiRun(0, text.Length, 0)], runs);
        Assert.All(levels, level => Assert.Equal(0, level));
        Assert.All(Enumerable.Range(0, text.Length), index => Assert.Equal(0, paragraph.GetLevel(index)));
    }

    [Fact]
    public void SetsLeftToRightTextAtLevelTwoInARightToLeftParagraph()
    {
        BidiParagraph paragraph = new BidiParagraph("abc".AsSpan(), BidiDirection.RightToLeft);

        Assert.Equal(1, paragraph.ParagraphLevel);
        Assert.False(paragraph.IsLeftToRightOnly);
        Assert.Equal(2, paragraph.GetLevel(0));
    }

    [Theory]
    [InlineData("a\u2069b")]
    [InlineData("a\u202A\u202Cb\u202C")]
    public void RecognisesLeftToRightTextThatNeededResolving(string text) =>
        Assert.True(new BidiParagraph(text.AsSpan()).IsLeftToRightOnly);

    [Theory]
    [InlineData("a\u0590")] // Unassigned, but in a block kept for right-to-left scripts.
    [InlineData("a\u05D0")]
    [InlineData("a\u0661")] // An Arabic-Indic digit: an Arabic number.
    [InlineData("a\u202Ab")]
    [InlineData("a\u202Bb")]
    [InlineData("a\u2066b\u2069")]
    [InlineData("a\U00010900")]
    public void ResolvesTextWithAnyRightToLeftCharacterOrControl(string text) =>
        Assert.False(new BidiParagraph(text.AsSpan()).IsLeftToRightOnly);

    [Fact]
    public void SetsMixedLatinAndArabicTextInRunsOfEachDirection()
    {
        // "Hello", an Arabic word, "world": the Arabic word is reversed in place between the two Latin ones.
        const string Text = "Hello \u0645\u0631\u062D\u0628\u0627 world";
        BidiParagraph paragraph = new BidiParagraph(Text.AsSpan());
        List<BidiRun> runs = new List<BidiRun>();

        paragraph.GetVisualRuns(0, Text.Length, runs);

        Assert.Equal(0, paragraph.ParagraphLevel);
        Assert.Equal([new BidiRun(0, 6, 0), new BidiRun(6, 5, 1), new BidiRun(11, 6, 0)], runs);
    }

    [Fact]
    public void GivesBothHalvesOfASurrogatePairTheLevelOfTheirCharacter()
    {
        // U+10900 and U+10901, Phoenician letters, are right-to-left and outside the Basic Multilingual Plane.
        const string Text = "ab \U00010900\U00010901 cd";
        BidiParagraph paragraph = new BidiParagraph(Text.AsSpan());
        List<BidiRun> runs = new List<BidiRun>();

        paragraph.GetVisualRuns(0, Text.Length, runs);

        Assert.Equal([0, 0, 0, 1, 1, 1, 1, 0, 0, 0], Enumerable.Range(0, Text.Length).Select(paragraph.GetLevel));
        Assert.Equal([new BidiRun(0, 3, 0), new BidiRun(3, 4, 1), new BidiRun(7, 3, 0)], runs);
    }

    [Fact]
    public void ReadsTheDirectionOfASupplementaryCharacterFromItsSurrogatePair()
    {
        Assert.Equal(1, new BidiParagraph("\U0001E900".AsSpan()).ParagraphLevel);
        Assert.Equal(1, new BidiParagraph("\U0001E900 \uD800".AsSpan()).ParagraphLevel);

        // A surrogate without its other half is a character of its own, and a left-to-right one.
        Assert.Equal(2, new BidiParagraph("\uDC00\U0001E900".AsSpan(), BidiDirection.RightToLeft).GetLevel(0));
        Assert.Equal(0, new BidiParagraph("\uD800\u05D0".AsSpan()).ParagraphLevel);
    }

    [Fact]
    public void ReordersLinesLongerThanTheStackBuffer()
    {
        string text = string.Concat(Enumerable.Repeat("ABC def ", 50));
        BidiParagraph paragraph = Paragraph(text, BidiDirection.LeftToRight);
        List<BidiRun> runs = new List<BidiRun>();

        paragraph.GetVisualRuns(0, text.Length, runs);

        Assert.Equal(100, runs.Count);
        Assert.Equal(new BidiRun(0, 3, 1), runs[0]);
        Assert.Equal(new BidiRun(3, 5, 0), runs[1]);
        Assert.Equal(text.Length, runs.Sum(run => run.Length));
    }

    [Fact]
    public void HasNoRunsForAnEmptyLine()
    {
        List<BidiRun> runs = [new BidiRun(0, 1, 0)];

        new BidiParagraph(string.Empty.AsSpan()).GetVisualRuns(0, 0, runs);
        Assert.Empty(runs);

        runs.Add(new BidiRun(0, 1, 0));
        Paragraph("ABC").GetVisualRuns(3, 0, runs);
        Assert.Empty(runs);

        BidiParagraph empty = new BidiParagraph(string.Empty.AsSpan(), BidiDirection.RightToLeft);
        Assert.True(empty.IsLeftToRightOnly);
        empty.GetVisualRuns(0, 0, runs);
        Assert.Empty(runs);
    }

    [Fact]
    public void RejectsLinesOutsideTheParagraph()
    {
        foreach (BidiParagraph paragraph in new[] { new BidiParagraph("abc".AsSpan()), Paragraph("ABC") })
        {
            List<BidiRun> runs = new List<BidiRun>();
            byte[] levels = new byte[4];

            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetVisualRuns(-1, 1, runs));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetVisualRuns(4, 0, runs));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetVisualRuns(0, 4, runs));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetVisualRuns(1, 3, runs));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetVisualRuns(1, -1, runs));
            Assert.Throws<ArgumentNullException>(() => paragraph.GetVisualRuns(0, 1, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetLineLevels(2, 2, levels));
            Assert.Throws<ArgumentException>(() => paragraph.GetLineLevels(0, 3, new byte[2]));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetLevel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.GetLevel(3));

            paragraph.GetVisualRuns(1, 2, runs);
            paragraph.GetLineLevels(3, 0, levels);
            Assert.Equal(2, runs.Sum(run => run.Length));
        }
    }

#if NET
    [Fact]
    public void AllocatesNothingPerCharacterForLeftToRightText()
    {
        string text = string.Concat(Enumerable.Repeat("The quick brown fox, 1.5% faster. ", 300));
        List<BidiRun> runs = new List<BidiRun>(4);
        new BidiParagraph(text.AsSpan()).GetVisualRuns(0, text.Length, runs);

        long before = GC.GetAllocatedBytesForCurrentThread();
        BidiParagraph paragraph = new BidiParagraph(text.AsSpan());
        paragraph.GetVisualRuns(0, text.Length, runs);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= 64, $"{allocated} bytes allocated for {text.Length} characters.");
    }

    [Theory]
    [InlineData(19)]
    [InlineData(256)]
    public void ReordersALineOfUpTo256CharactersWithoutAllocating(int length)
    {
        string text = string.Concat(Enumerable.Repeat("abc DEF ghi JKL mno ", 13)).Substring(0, length);
        BidiParagraph paragraph = Paragraph(text);
        List<BidiRun> runs = new List<BidiRun>(128);
        paragraph.GetVisualRuns(0, length, runs);

        long before = GC.GetAllocatedBytesForCurrentThread();
        paragraph.GetVisualRuns(0, length, runs);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(length, runs.Sum(run => run.Length));
    }
#endif
}
