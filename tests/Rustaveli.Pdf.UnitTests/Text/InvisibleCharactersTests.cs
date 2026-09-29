namespace Rustaveli.Pdf.UnitTests.Shaping;

public class InvisibleCharactersTests
{
    /// <summary>The first and last character of every range that is not drawn.</summary>
    public static TheoryData<int> Invisible => new TheoryData<int>
    {
        0x0000, 0x0008, 0x000A, 0x001F, 0x007F, 0x009F, 0x00AD, 0x034F, 0x061C, 0x17B4, 0x17B5, 0x180B, 0x180F, 0x200B,
        0x200F, 0x202A, 0x202E, 0x2060, 0x206F, 0xFE00, 0xFE0F, 0xFEFF, 0xFFF0, 0xFFF8, 0x1D173, 0x1D17A, 0xE0000,
        0xE0FFF,
    };

    /// <summary>The characters just outside those ranges, a tab, and the Hangul fillers, which fonts draw.</summary>
    public static TheoryData<int> Visible => new TheoryData<int>
    {
        0x0009, 0x0020, 0x007E, 0x00A0, 0x00AC, 0x00AE, 0x034E, 0x0350, 0x061B, 0x061D, 0x115F, 0x17B3, 0x17B6, 0x180A,
        0x1810, 0x200A, 0x2010, 0x2029, 0x202F, 0x205F, 0x2070, 0x3164, 0xFDFF, 0xFE10, 0xFEFE, 0xFF00, 0xFFA0, 0xFFEF,
        0xFFF9, 0x1D172, 0x1D17B, 0xDFFFF, 0xE1000,
    };

    [Theory]
    [MemberData(nameof(Invisible))]
    public void IsInvisible(int codepoint) => Assert.True(InvisibleCharacters.Contains(codepoint), $"U+{codepoint:X4}");

    [Theory]
    [MemberData(nameof(Visible))]
    public void IsDrawn(int codepoint) => Assert.False(InvisibleCharacters.Contains(codepoint), $"U+{codepoint:X4}");
}
