namespace Rustaveli.Pdf.UnitTests.Shaping;

public class GraphemeBoundariesTests
{
    /// <summary>The clusters <paramref name="text"/> falls into, found one after another from its start.</summary>
    private static List<string> Clusters(string text)
    {
        List<string> clusters = [];

        for (int start = 0; start < text.Length;)
        {
            int length = GraphemeBoundaries.FirstLength(text.AsSpan(start));
            clusters.Add(text.Substring(start, length));
            start += length;
        }

        return clusters;
    }

    [Theory]
    [InlineData("ab", new[] { "a", "b" })]
    [InlineData("éx", new[] { "é", "x" })]
    [InlineData("\U00020BB7\U00020BB7", new[] { "\U00020BB7", "\U00020BB7" })]
    [InlineData("a\U0001D167b", new[] { "a\U0001D167", "b" })]
    [InlineData("\U0001E900\U0001E944", new[] { "\U0001E900\U0001E944" })]
    [InlineData("กิ", new[] { "กิ" })]
    [InlineData("❤️", new[] { "❤️" })]
    [InlineData("\U0001F44D\U0001F3FD!", new[] { "\U0001F44D\U0001F3FD", "!" })]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467", new[] { "\U0001F468‍\U0001F469‍\U0001F467" })]
    [InlineData("❤‍❤", new[] { "❤‍❤" })]
    [InlineData("a‍b", new[] { "a‍", "b" })]
    [InlineData("\U0001F1EC\U0001F1EA\U0001F1FA\U0001F1F8\U0001F1EB", new[] { "\U0001F1EC\U0001F1EA", "\U0001F1FA\U0001F1F8", "\U0001F1EB" })]
    [InlineData("a\u0001", new[] { "a", "\u0001" })]
    [InlineData("́a", new[] { "́", "a" })]
    public void TextFallsIntoTheClustersAReaderSees(string text, string[] clusters) =>
        Assert.Equal(clusters, Clusters(text));

    // Not theory data, which cannot carry a lone surrogate intact.
    [Fact]
    public void ASurrogateWithoutItsPartnerIsAClusterOfItsOwn()
    {
        Assert.Equal(["\uD842", "a"], Clusters("\uD842a"));
        Assert.Equal(["a", "\uD842"], Clusters("a\uD842"));
    }

    [Fact]
    public void NoTextHasNoCluster() => Assert.Equal(0, GraphemeBoundaries.FirstLength(ReadOnlySpan<char>.Empty));
}
