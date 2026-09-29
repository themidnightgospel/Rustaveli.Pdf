namespace Rustaveli.Pdf.UnitTests.Shaping;

public class JoinedScriptsTests
{
    /// <summary>The first and last character of every block written joined.</summary>
    public static TheoryData<int> Joined => new TheoryData<int>
    {
        0x0600, 0x077F, 0x07C0, 0x07FF, 0x0840, 0x08FF, 0x1800, 0x18AF, 0xFB50, 0xFDFF, 0xFE70, 0xFEFE, 0x10AC0, 0x10AFF,
        0x10B80, 0x10BAF, 0x10D00, 0x10D3F, 0x10F30, 0x10FAF, 0x1E900, 0x1E95F,
    };

    /// <summary>The characters just outside those blocks: Thaana and Samaritan, which are not joined, among them.</summary>
    public static TheoryData<int> Separate => new TheoryData<int>
    {
        'a', 0x05FF, 0x0780, 0x07BF, 0x0800, 0x083F, 0x0900, 0x17FF, 0x18B0, 0xFB4F, 0xFE00, 0xFE6F, 0xFEFF, 0x10ABF,
        0x10B00, 0x10B7F, 0x10BB0, 0x10CFF, 0x10D40, 0x10F2F, 0x10FB0, 0x1E8FF, 0x1E960,
    };

    [Theory]
    [MemberData(nameof(Joined))]
    public void IsJoined(int codepoint) => Assert.True(JoinedScripts.Contains(codepoint), $"U+{codepoint:X4}");

    [Theory]
    [MemberData(nameof(Separate))]
    public void IsNotJoined(int codepoint) => Assert.False(JoinedScripts.Contains(codepoint), $"U+{codepoint:X4}");
}
