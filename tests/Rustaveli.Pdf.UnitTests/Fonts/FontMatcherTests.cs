using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>The CSS Fonts Level 4 matching rules, each step against a family built to exercise it.</summary>
public class FontMatcherTests
{
    private static FontFaceInfo Face(
        int weight, FontSlant slant = FontSlant.Upright, int width = 5, string name = "") =>
        new FontFaceInfo(
            new FontFileSource(ReadOnlyMemory<byte>.Empty),
            0,
            new FontNames("Family", name, name, null, null, null, ["Family"], []),
            new FaceStyle(weight, width, slant),
            OutlineFormat.TrueType,
            registered: false);

    private static int SelectWeight(int requested, params int[] available) =>
        FontMatcher.Select(available.Select(weight => Face(weight)), new FaceStyle(requested, 5, FontSlant.Upright))!
            .Style.Weight;

    [Theory]
    [InlineData(400, 400)]
    [InlineData(450, 500)]
    [InlineData(500, 500)]
    [InlineData(300, 300)]
    [InlineData(600, 700)]
    [InlineData(900, 900)]
    public void PicksAnExactWeightOrTheCssNeighbour(int requested, int expected)
    {
        Assert.Equal(expected, SelectWeight(requested, 300, 400, 500, 700, 900));
    }

    [Fact]
    public void BetweenFourAndFiveHundredLooksHeavierUpToFiveHundredThenLighter()
    {
        Assert.Equal(500, SelectWeight(420, 300, 500, 600));
        Assert.Equal(300, SelectWeight(420, 300, 600));
        Assert.Equal(600, SelectWeight(420, 600, 700));
    }

    [Fact]
    public void BelowFourHundredLooksLighterFirst()
    {
        Assert.Equal(200, SelectWeight(300, 100, 200, 400));
        Assert.Equal(400, SelectWeight(300, 400, 500));
    }

    [Fact]
    public void AboveFiveHundredLooksHeavierFirst()
    {
        Assert.Equal(800, SelectWeight(600, 400, 800, 900));
        Assert.Equal(500, SelectWeight(600, 300, 500));
    }

    // Slants as integers — 0 upright, 1 italic, 2 oblique — since the enum is internal to the library.
    [Theory]
    [InlineData(1, new[] { 0, 2 }, 2)]
    [InlineData(2, new[] { 0, 1 }, 1)]
    [InlineData(0, new[] { 1, 2 }, 2)]
    [InlineData(1, new[] { 0 }, 0)]
    [InlineData(0, new[] { 1 }, 1)]
    public void FallsBackBetweenSlantsAsCssDoes(int requested, int[] available, int expected)
    {
        IEnumerable<FontFaceInfo> faces = available.Select(slant => Face(400, (FontSlant)slant));
        FontFaceInfo? face = FontMatcher.Select(faces, new FaceStyle(400, 5, (FontSlant)requested));

        Assert.Equal((FontSlant)expected, face!.Style.Slant);
    }

    [Fact]
    public void PrefersTheRightSlantOverTheRightWeight()
    {
        FontFaceInfo[] faces = [Face(700), Face(400, FontSlant.Italic)];

        FontFaceInfo? face = FontMatcher.Select(faces, new FaceStyle(700, 5, FontSlant.Italic));

        Assert.Equal(FontSlant.Italic, face!.Style.Slant);
    }

    [Fact]
    public void PrefersTheRightWidthOverTheRightSlant()
    {
        FontFaceInfo[] faces = [Face(400, FontSlant.Italic, width: 3), Face(400, FontSlant.Upright)];

        Assert.Equal(5, FontMatcher.Select(faces, new FaceStyle(400, 5, FontSlant.Italic))!.Style.Width);
    }

    [Theory]
    [InlineData(5, new[] { 3, 4, 7 }, 4)]
    [InlineData(5, new[] { 6, 8 }, 6)]
    [InlineData(3, new[] { 2, 4 }, 2)]
    [InlineData(7, new[] { 6, 8 }, 8)]
    [InlineData(7, new[] { 2, 6 }, 6)]
    public void LooksNarrowerForNormalWidthsAndWiderForExpandedOnes(int requested, int[] available, int expected)
    {
        IEnumerable<FontFaceInfo> faces = available.Select(width => Face(400, width: width));
        FontFaceInfo? face = FontMatcher.Select(faces, new FaceStyle(400, requested, FontSlant.Upright));

        Assert.Equal(expected, face!.Style.Width);
    }

    [Fact]
    public void BreaksTiesByOrder()
    {
        FontFaceInfo[] faces = [Face(400, name: "first"), Face(400, name: "second")];

        Assert.Equal("first", FontMatcher.Select(faces, new FaceStyle(400, 5, FontSlant.Upright))!.Names.Subfamily);
    }

    [Fact]
    public void SelectsNothingFromNoFaces()
    {
        Assert.Null(FontMatcher.Select([], new FaceStyle(400, 5, FontSlant.Upright)));
    }
}
