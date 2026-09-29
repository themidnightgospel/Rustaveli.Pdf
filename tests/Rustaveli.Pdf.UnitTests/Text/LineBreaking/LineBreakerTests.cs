using System.Text;
using Rustaveli.Pdf.Text.LineBreaking;

namespace Rustaveli.Pdf.UnitTests.LineBreaking;

/// <summary>
/// One or more cases per rule of UAX #14, each chosen so that the rule is what decides it: without the rule, or with
/// a rule before it misapplied, the expected breaks would differ. Most assertions are on the text marked with ÷
/// wherever a line may end, leaving out the break every text has at its end.
/// </summary>
public class LineBreakerTests
{
    private const string Ideograph = "\u4E2D";
    private const string AnotherIdeograph = "\u6587";

    [Fact]
    public void FindsNoBreakInEmptyText()
    {
        Assert.Empty(Breaks(""));
    }

    [Fact]
    public void EndsEveryOtherTextWithAMandatoryBreak()
    {
        Assert.Equal([new LineBreak(4, IsMandatory: true)], Breaks("word"));
    }

    [Fact]
    public void NeverBreaksBeforeTheFirstCharacter()
    {
        Assert.Equal([new LineBreak(1, false), new LineBreak(5, true)], Breaks(" word"));
        Assert.Equal([new LineBreak(1, false), new LineBreak(5, true)], Breaks("\u200Bword"));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\u0085")]
    [InlineData("\u000B")]
    [InlineData("\u000C")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void ForcesABreakAfterEveryKindOfHardLineBreak(string lineBreak)
    {
        Assert.Equal([new LineBreak(2, true), new LineBreak(3, true)], Breaks("a" + lineBreak + "b"));
    }

    [Fact]
    public void KeepsACarriageReturnWithTheLineFeedAfterIt()
    {
        Assert.Equal([new LineBreak(3, true), new LineBreak(4, true)], Breaks("a\r\nb"));
        Assert.Equal([new LineBreak(2, true), new LineBreak(3, true)], Breaks("a\r\r"));
    }

    [Fact]
    public void DoesNotBreakBeforeAHardLineBreak()
    {
        Assert.Equal([new LineBreak(3, true), new LineBreak(4, true), new LineBreak(5, true)], Breaks("a \n\nb"));
    }

    [Fact]
    public void MarksOnlyHardLineBreaksAndTheEndAsMandatory()
    {
        Assert.Equal([new LineBreak(2, false), new LineBreak(4, true), new LineBreak(5, true)], Breaks("a b\nc"));
    }

    [Fact]
    public void KeepsSpacesAndZeroWidthSpacesWithWhatPrecedesThem()
    {
        Assert.Equal("one ÷two  ÷three", Marked("one two  three"));
        Assert.Equal("a \u200B ÷b", Marked("a \u200B b"));
    }

    [Fact]
    public void BreaksAfterAZeroWidthSpaceEvenWhereLaterRulesWouldNot()
    {
        Assert.Equal("a\u200B÷)", Marked("a\u200B)"));
        Assert.Equal("a\u200B  ÷)", Marked("a\u200B  )"));
        Assert.Equal("a\u200B÷\u0301b", Marked("a\u200B\u0301b"));
    }

    [Fact]
    public void DoesNotBreakAfterAZeroWidthJoiner()
    {
        Assert.Equal("a÷" + Ideograph, Marked("a" + Ideograph));
        Assert.Equal("a\u200D" + Ideograph + "÷" + Ideograph, Marked("a\u200D" + Ideograph + Ideograph));
        Assert.Equal(" ÷\u200D" + Ideograph, Marked(" \u200D" + Ideograph));
        Assert.Equal("\u200D" + Ideograph, Marked("\u200D" + Ideograph));
    }

    [Fact]
    public void KeepsAnEmojiZeroWidthJoinerSequenceTogether()
    {
        // Woman, joiner, laptop: one technologist.
        Assert.Equal("\U0001F469\u200D\U0001F4BB", Marked("\U0001F469\u200D\U0001F4BB"));
    }

    [Fact]
    public void KeepsCombiningMarksWithTheCharacterBeforeThem()
    {
        Assert.Equal(Ideograph + "\u0301÷" + Ideograph, Marked(Ideograph + "\u0301" + Ideograph));
        Assert.Equal("a\u0301\u0302b", Marked("a\u0301\u0302b"));
    }

    [Fact]
    public void JudgesACombiningSequenceAsItsBaseCharacter()
    {
        Assert.Equal("a\u0301(", Marked("a\u0301("));
        Assert.Equal(Ideograph + "\u0301÷(", Marked(Ideograph + "\u0301("));
        Assert.Equal("(\u0301 " + Ideograph, Marked("(\u0301 " + Ideograph));
    }

    [Fact]
    public void TreatsAMarkWithNothingToAttachToAsALetter()
    {
        Assert.Equal("a ÷\u0301b", Marked("a \u0301b"));
        Assert.Equal("\u0301a", Marked("\u0301a"));
        Assert.Equal([new LineBreak(2, true), new LineBreak(4, true)], Breaks("a\n\u0301b"));
    }

    [Fact]
    public void TreatsAnOrphanedWideMarkAsANarrowLetter()
    {
        // U+302A is a wide combining mark. Made a letter by LB10, it is not East Asian, so LB19a keeps the opening
        // quotation mark after it.
        Assert.Equal(Ideograph + " ÷\u302A\u201C" + Ideograph, Marked(Ideograph + " \u302A\u201C" + Ideograph));
    }

    [Fact]
    public void NeverBreaksNextToAWordJoiner()
    {
        Assert.Equal(Ideograph + "\u2060" + Ideograph, Marked(Ideograph + "\u2060" + Ideograph));
        Assert.Equal("a \u2060b", Marked("a \u2060b"));
    }

    [Fact]
    public void DoesNotBreakAfterANoBreakSpace()
    {
        Assert.Equal(Ideograph + "÷" + Ideograph, Marked(Ideograph + Ideograph));
        Assert.Equal(Ideograph + "\u00A0" + Ideograph, Marked(Ideograph + "\u00A0" + Ideograph));
    }

    [Fact]
    public void BreaksBeforeANoBreakSpaceOnlyAfterASpaceOrHyphen()
    {
        Assert.Equal("x-÷\u00A0y", Marked("x-\u00A0y"));
        Assert.Equal("x ÷\u00A0y", Marked("x \u00A0y"));
        Assert.Equal("x\u2010÷\u00A0y", Marked("x\u2010\u00A0y"));
        Assert.Equal("x\u00A0y", Marked("x\u00A0y"));
    }

    [Theory]
    [InlineData(")")]
    [InlineData("]")]
    [InlineData("!")]
    [InlineData("/")]
    [InlineData("\u300D")]
    public void DoesNotBreakBeforeClosingPunctuationEvenAfterSpaces(string closing)
    {
        Assert.Equal(Ideograph + " " + closing, Marked(Ideograph + " " + closing));
        Assert.Equal(Ideograph + closing, Marked(Ideograph + closing));
    }

    [Fact]
    public void DoesNotBreakAfterOpeningPunctuationEvenAfterSpaces()
    {
        Assert.Equal("(  " + Ideograph, Marked("(  " + Ideograph));
        Assert.Equal("a  ÷" + Ideograph, Marked("a  " + Ideograph));
    }

    [Fact]
    public void KeepsAnInitialQuotationMarkThatOpensAQuotationWithWhatFollows()
    {
        Assert.Equal("\u201C " + Ideograph, Marked("\u201C " + Ideograph));
        Assert.Equal("x ÷\u201C " + Ideograph, Marked("x \u201C " + Ideograph));
        Assert.Equal("(\u201C " + Ideograph, Marked("(\u201C " + Ideograph));
        Assert.Equal("a\u201C ÷" + Ideograph, Marked("a\u201C " + Ideograph));
    }

    [Theory]
    [InlineData("\u2028")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\u0085")]
    [InlineData(" ")]
    [InlineData("\u200B")]
    [InlineData("\u00A0")]
    [InlineData("(")]
    [InlineData("\"")]
    [InlineData("\u201C")]
    [InlineData("\u201D")]
    public void AnInitialQuotationMarkOpensAQuotationAfterEveryClassTheRuleNames(string before)
    {
        Assert.EndsWith("\u201C " + Ideograph, Marked(before + "\u201C " + Ideograph));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("\u2060")]
    public void AnInitialQuotationMarkAfterAnythingElseOpensNoQuotation(string before)
    {
        Assert.EndsWith("\u201C ÷" + Ideograph, Marked(before + "\u201C " + Ideograph));
    }

    [Theory]
    [InlineData("\u2028")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\u0085")]
    [InlineData(" ")]
    [InlineData("\u200B")]
    [InlineData("\u2060")]
    [InlineData("\u00A0")]
    [InlineData("}")]
    [InlineData(")")]
    [InlineData("!")]
    [InlineData(",")]
    [InlineData("/")]
    [InlineData("\"")]
    [InlineData("\u201C")]
    [InlineData("\u201D")]
    public void AFinalQuotationMarkClosesAQuotationBeforeEveryClassTheRuleNames(string after)
    {
        Assert.StartsWith(Ideograph + " \u201D", Marked(Ideograph + " \u201D" + after));
    }

    [Fact]
    public void KeepsAFinalQuotationMarkThatClosesAQuotationWithWhatPrecedes()
    {
        Assert.Equal(Ideograph + " \u201D", Marked(Ideograph + " \u201D"));
        Assert.Equal(Ideograph + " \u201D\u0301", Marked(Ideograph + " \u201D\u0301"));
        Assert.Equal(Ideograph + " \u201D.", Marked(Ideograph + " \u201D."));
        Assert.Equal(Ideograph + " ÷\u201D" + Ideograph, Marked(Ideograph + " \u201D" + Ideograph));
    }

    [Fact]
    public void BreaksBeforeADecimalMarkThatFollowsASpace()
    {
        Assert.Equal("subtract ÷.5", Marked("subtract .5"));
        Assert.Equal("subtract ÷.\u03015", Marked("subtract .\u03015"));
        Assert.Equal("a .b", Marked("a .b"));
    }

    [Fact]
    public void DoesNotBreakBeforeACommaOrFullStopEvenAfterSpaces()
    {
        Assert.Equal(Ideograph + " ,÷" + Ideograph, Marked(Ideograph + " ," + Ideograph));
        Assert.Equal(Ideograph + ".", Marked(Ideograph + "."));
    }

    [Fact]
    public void KeepsANonstarterWithClosingPunctuationEvenAcrossSpaces()
    {
        Assert.Equal(Ideograph + ") \u3041", Marked(Ideograph + ") \u3041"));
        Assert.Equal(Ideograph + "\u300D \u3041", Marked(Ideograph + "\u300D \u3041"));
        Assert.Equal(Ideograph + " ÷\u3041", Marked(Ideograph + " \u3041"));
    }

    [Fact]
    public void KeepsEmDashesTogetherEvenAcrossSpaces()
    {
        Assert.Equal("\u2014\u2014", Marked("\u2014\u2014"));
        Assert.Equal("\u2014 \u2014", Marked("\u2014 \u2014"));
        Assert.Equal("\u2014 ÷a", Marked("\u2014 a"));
        Assert.Equal("a÷\u2014÷b", Marked("a\u2014b"));
    }

    [Fact]
    public void DoesNotBreakAroundAQuotationMarkThatIsNeitherInitialNorFinal()
    {
        Assert.Equal(Ideograph + "\"" + Ideograph, Marked(Ideograph + "\"" + Ideograph));
    }

    [Fact]
    public void BreaksOutsideCurlyQuotationMarksInEastAsianText()
    {
        Assert.Equal(
            Ideograph + "÷\u201C" + Ideograph + "\u201D÷" + Ideograph,
            Marked(Ideograph + "\u201C" + Ideograph + "\u201D" + Ideograph));
    }

    [Fact]
    public void KeepsCurlyQuotationMarksWithNeighboursThatAreNotEastAsian()
    {
        Assert.Equal("a\u201Cb\u201Dc", Marked("a\u201Cb\u201Dc"));
        Assert.Equal(Ideograph + "\u201Ca", Marked(Ideograph + "\u201Ca"));
        Assert.Equal(Ideograph + "\u201C", Marked(Ideograph + "\u201C"));
        Assert.Equal("a\u201D" + Ideograph, Marked("a\u201D" + Ideograph));
        Assert.Equal(Ideograph + "\u201Da", Marked(Ideograph + "\u201Da"));
    }

    [Fact]
    public void BreaksAroundAnObjectReplacementCharacter()
    {
        Assert.Equal("a÷\uFFFC÷b", Marked("a\uFFFCb"));
    }

    [Fact]
    public void KeepsAHyphenThatStartsAWordWithTheWord()
    {
        Assert.Equal("-a", Marked("-a"));
        Assert.Equal("x ÷-a", Marked("x -a"));
        Assert.Equal("x-÷a", Marked("x-a"));
        Assert.Equal("x ÷\u2010a", Marked("x \u2010a"));
        Assert.Equal("x\u2010÷a", Marked("x\u2010a"));
        Assert.Equal("x ÷-÷" + Ideograph, Marked("x -" + Ideograph));
    }

    [Fact]
    public void DoesNotBreakBeforeHyphensOrNonstartersOrAfterAcuteAccents()
    {
        Assert.Equal("a-÷b", Marked("a-b"));
        Assert.Equal(Ideograph + "\u3005", Marked(Ideograph + "\u3005"));
        Assert.Equal("a÷\u00B4b", Marked("a\u00B4b"));
    }

    [Fact]
    public void KeepsTheHyphenAfterHebrewWithTheNonHebrewTextAfterIt()
    {
        Assert.Equal("\u05D0-a", Marked("\u05D0-a"));
        Assert.Equal("\u05D0\u2010a", Marked("\u05D0\u2010a"));
        Assert.Equal("\u05D0-÷\u05D0", Marked("\u05D0-\u05D0"));

        // An ideographic space is a break-after character too, but East Asian, which the rule excludes.
        Assert.Equal("\u05D0\u3000÷a", Marked("\u05D0\u3000a"));
    }

    [Fact]
    public void DoesNotBreakBetweenASolidusAndHebrew()
    {
        Assert.Equal("a/\u05D0", Marked("a/\u05D0"));
        Assert.Equal("a/÷b", Marked("a/b"));
    }

    [Fact]
    public void DoesNotBreakBeforeAnEllipsis()
    {
        Assert.Equal(Ideograph + "\u2026", Marked(Ideograph + "\u2026"));
    }

    [Fact]
    public void DoesNotBreakBetweenDigitsAndLetters()
    {
        Assert.Equal("a1b2", Marked("a1b2"));
        Assert.Equal("\u05D01\u05D0", Marked("\u05D01\u05D0"));
    }

    [Fact]
    public void KeepsNumericPrefixesAndPostfixesWithIdeographs()
    {
        Assert.Equal("$" + Ideograph, Marked("$" + Ideograph));
        Assert.Equal(Ideograph + "%", Marked(Ideograph + "%"));
        Assert.Equal(Ideograph + "÷$", Marked(Ideograph + "$"));
        Assert.Equal("$\U0001F44D", Marked("$\U0001F44D"));
        Assert.Equal("\U0001F44D%", Marked("\U0001F44D%"));
    }

    [Theory]
    [InlineData("$a")]
    [InlineData("%a")]
    [InlineData("a%")]
    [InlineData("a$")]
    [InlineData("\u05D0%")]
    public void KeepsNumericPrefixesAndPostfixesWithLetters(string text)
    {
        Assert.Equal(text, Marked(text));
    }

    [Theory]
    [InlineData("$(12.35)")]
    [InlineData("2,1234")]
    [InlineData("(12)\u00A2")]
    [InlineData("12.54\u00A2")]
    [InlineData(".50")]
    [InlineData("\u20B91,00,000.00")]
    [InlineData("-1/12")]
    [InlineData("$(.5")]
    [InlineData("$(\u03015")]
    [InlineData("$(.\u03015")]
    [InlineData("1)%")]
    [InlineData("12%")]
    [InlineData("$12")]
    [InlineData("%12")]
    public void DoesNotBreakInsideNumbers(string number)
    {
        Assert.Equal(number, Marked(number));
    }

    [Fact]
    public void BreaksBetweenWhatOnlyLooksLikeANumber()
    {
        Assert.Equal("a/÷1", Marked("a/1"));
        Assert.Equal("1a/÷2", Marked("1a/2"));
        Assert.Equal("$÷(a", Marked("$(a"));
        Assert.Equal("$÷(.a", Marked("$(.a"));
        Assert.Equal("a)÷%", Marked("a)%"));
        Assert.Equal("1a)÷%", Marked("1a)%"));
    }

    [Fact]
    public void DoesNotBreakKoreanSyllables()
    {
        Assert.Equal("\u1100\u1161\u11A8", Marked("\u1100\u1161\u11A8"));
        Assert.Equal("\u1100\uAC00", Marked("\u1100\uAC00"));
        Assert.Equal("\uAC00\u11A8", Marked("\uAC00\u11A8"));
        Assert.Equal("\uAC01\u11A8", Marked("\uAC01\u11A8"));
        Assert.Equal("\uAC00÷\uAC01", Marked("\uAC00\uAC01"));
    }

    [Fact]
    public void KeepsNumericPrefixesAndPostfixesWithKoreanSyllables()
    {
        Assert.Equal("\uAC00%", Marked("\uAC00%"));
        Assert.Equal("$\uAC00", Marked("$\uAC00"));
        Assert.Equal("\u11A8%", Marked("\u11A8%"));
    }

    [Fact]
    public void DoesNotBreakBetweenLetters()
    {
        Assert.Equal("word\u05D0\u05D1", Marked("word\u05D0\u05D1"));
    }

    [Fact]
    public void DoesNotBreakInsideTheOrthographicSyllablesOfBrahmicScripts()
    {
        // Balinese ka and adeg adeg, a virama; Batak a and pangolat, a final virama; Brahmi jihvamuliya, a
        // pre-base sign, and ka; the dotted circle standing in for a consonant.
        Assert.Equal("\u1B13\u1B44\u1B13", Marked("\u1B13\u1B44\u1B13"));
        Assert.Equal("\u1B13\u1BF2", Marked("\u1B13\u1BF2"));
        Assert.Equal("\u1BC0\u1BC0\u1BF2", Marked("\u1BC0\u1BC0\u1BF2"));
        Assert.Equal("\u1BC0\u1B13\u0301\u1BF2", Marked("\u1BC0\u1B13\u0301\u1BF2"));
        Assert.Equal("\U00011003\U00011013", Marked("\U00011003\U00011013"));
        Assert.Equal("\u25CC\u1B44\u1B13", Marked("\u25CC\u1B44\u1B13"));
        Assert.Equal("\u1B13\u1B44\u25CC", Marked("\u1B13\u1B44\u25CC"));
    }

    [Fact]
    public void BreaksBetweenBrahmicSyllables()
    {
        Assert.Equal("\u1B13÷\u1B13", Marked("\u1B13\u1B13"));
        Assert.Equal("\u1B13\u1B44÷\u1BC0", Marked("\u1B13\u1B44\u1BC0"));
        Assert.Equal("a÷\u1B44÷\u1B13", Marked("a\u1B44\u1B13"));
        Assert.Equal("\u1B13÷\u1B13÷a", Marked("\u1B13\u1B13a"));
        Assert.Equal("a÷\u1B13", Marked("a\u1B13"));
    }

    [Fact]
    public void DoesNotBreakBetweenNumericPunctuationAndLetters()
    {
        Assert.Equal("e.g.", Marked("e.g."));
        Assert.Equal(Ideograph + ".÷" + Ideograph, Marked(Ideograph + "." + Ideograph));
    }

    [Fact]
    public void KeepsParenthesesWithTheWordTheyEnclose()
    {
        Assert.Equal("person(s)", Marked("person(s)"));
        Assert.Equal("(s)a", Marked("(s)a"));
        Assert.Equal("1(2)3", Marked("1(2)3"));
    }

    [Fact]
    public void BreaksOutsideEastAsianParentheses()
    {
        Assert.Equal("a÷\uFF08b\uFF09÷c", Marked("a\uFF08b\uFF09c"));
    }

    [Fact]
    public void KeepsRegionalIndicatorsInPairs()
    {
        const string Georgia = "\U0001F1EC\U0001F1EA";
        const string States = "\U0001F1FA\U0001F1F8";

        Assert.Equal(Georgia + "÷" + States, Marked(Georgia + States));
        Assert.Equal(Georgia + "÷\U0001F1FA", Marked(Georgia + "\U0001F1FA"));
        Assert.Equal("a÷" + Georgia + "÷" + States, Marked("a" + Georgia + States));
        Assert.Equal("\U0001F1EC\u0301\U0001F1EA", Marked("\U0001F1EC\u0301\U0001F1EA"));
    }

    [Fact]
    public void KeepsAnEmojiWithItsSkinTone()
    {
        Assert.Equal("\U0001F44D\U0001F3FD", Marked("\U0001F44D\U0001F3FD"));
        Assert.Equal("\U0001FAFF\U0001F3FD", Marked("\U0001FAFF\U0001F3FD"));
        Assert.Equal(Ideograph + "÷\U0001F3FD", Marked(Ideograph + "\U0001F3FD"));
    }

    [Fact]
    public void BreaksBetweenIdeographs()
    {
        Assert.Equal(Ideograph + "÷" + AnotherIdeograph, Marked(Ideograph + AnotherIdeograph));
    }

    [Fact]
    public void CountsPositionsInUtf16CodeUnits()
    {
        Assert.Equal([new LineBreak(2, false), new LineBreak(4, true)], Breaks("\U00020000\U00020001"));
    }

    [Fact]
    public void TreatsASurrogateWithoutItsPartnerAsALetter()
    {
        Assert.Equal("a\uD800b", Marked("a\uD800b"));
        Assert.Equal("\uD800a", Marked("\uD800a"));
        Assert.Equal("a\uD800", Marked("a\uD800"));
        Assert.Equal("\uDC00÷" + Ideograph, Marked("\uDC00" + Ideograph));
    }

#if NET
    [Fact]
    public void AllocatesNothing()
    {
        const string Text = "An ordinary sentence, with punctuation \u2014 and 12.5% (or so) of numbers.\n";
        Assert.Equal(12, Count(Text));

        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = Count(Text);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(12, count);
        Assert.Equal(0, allocated);
    }

    private static int Count(string text)
    {
        int count = 0;

        foreach (LineBreak _ in LineBreaker.Enumerate(text.AsSpan()))
            count++;

        return count;
    }
#endif

    private static List<LineBreak> Breaks(string text)
    {
        List<LineBreak> breaks = [];

        foreach (LineBreak lineBreak in LineBreaker.Enumerate(text.AsSpan()))
            breaks.Add(lineBreak);

        return breaks;
    }

    /// <summary>The text with ÷ wherever a line may end, except at the end of the text.</summary>
    private static string Marked(string text)
    {
        StringBuilder marked = new StringBuilder();
        int start = 0;

        foreach (LineBreak lineBreak in LineBreaker.Enumerate(text.AsSpan()))
        {
            marked.Append(text, start, lineBreak.Position - start);

            if (lineBreak.Position < text.Length)
                marked.Append('÷');

            start = lineBreak.Position;
        }

        return marked.ToString();
    }
}
