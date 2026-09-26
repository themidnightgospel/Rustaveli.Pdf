namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Float comparisons with a tolerance, since layout accumulates rounding across nested transforms.
/// </summary>
internal static class Approximately
{
    public const float Tolerance = 0.01f;

    public static void Equal(float expected, float actual, string? because = null)
    {
        float difference = Math.Abs(expected - actual);

        Assert.True(
            difference <= Tolerance,
            $"Expected {expected} but found {actual} (difference {difference}).{(because is null ? "" : " " + because)}");
    }

    public static void Equal(Extent expected, Extent actual)
    {
        Equal(expected.Width, actual.Width, "Widths differ.");
        Equal(expected.Height, actual.Height, "Heights differ.");
    }

    public static void Equal(Offset expected, Offset actual)
    {
        Equal(expected.X, actual.X, "X coordinates differ.");
        Equal(expected.Y, actual.Y, "Y coordinates differ.");
    }
}
