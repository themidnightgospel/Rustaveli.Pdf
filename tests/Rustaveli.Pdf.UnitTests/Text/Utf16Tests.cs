namespace Rustaveli.Pdf.UnitTests.Shaping;

public class Utf16Tests
{
    [Fact]
    public void ASurrogatePairReadsAsOneCodepointTwoUnitsLong()
    {
        Assert.Equal(0x1F600, Utf16.CodepointAt("a\U0001F600", 1, out int length));
        Assert.Equal(2, length);
    }

    [Fact]
    public void ACharacterOfTheBasicPlaneReadsAsItselfOneUnitLong()
    {
        Assert.Equal('é', Utf16.CodepointAt("aé", 1, out int length));
        Assert.Equal(1, length);
    }

    // Not theory data, which cannot carry a lone surrogate intact.
    [Fact]
    public void ASurrogateWithoutItsPartnerReadsAsItself()
    {
        Assert.Equal(0xD83D, Utf16.CodepointAt("a\uD83D", 1, out int atTheEnd));
        Assert.Equal(0xD83D, Utf16.CodepointAt("\uD83Da", 0, out int beforeALetter));
        Assert.Equal(0xDE00, Utf16.CodepointAt("\uDE00\uD83D", 0, out int lowFirst));
        Assert.Equal(0xDE00, Utf16.CodepointAt("\U0001F600", 1, out int lowHalfOfAPair));

        Assert.Equal([1, 1, 1, 1], new[] { atTheEnd, beforeALetter, lowFirst, lowHalfOfAPair });
    }

    [Fact]
    public void TheLastCodepointIsTheLastAPairCanStandFor() =>
        Assert.Equal(Utf16.MaxCodepoint, Utf16.CodepointAt("\U0010FFFF", 0, out _));
}
