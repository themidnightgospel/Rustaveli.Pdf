namespace Rustaveli.Pdf;

/// <summary>
/// A width and a height in PDF points: the size of a page, the room a block is offered, or the room it takes.
/// </summary>
/// <remarks>
/// Layout adds and subtracts these sizes many times over on the way down a document tree, and single-precision
/// arithmetic drifts as it goes: a column three times a third of the page can come out a hair wider than the page.
/// Comparisons that decide whether content fits therefore allow <see cref="Epsilon"/> either way, so that drift never
/// pushes content onto another page.
/// </remarks>
public readonly record struct Extent(float Width, float Height)
{
    /// <summary>
    /// How far apart two lengths may be and still count as the same, in points: a thousandth of a point, far below
    /// anything a reader can see, and far above the drift of single-precision layout arithmetic.
    /// </summary>
    public const float Epsilon = 0.001f;

    /// <summary>No width and no height.</summary>
    public static Extent Zero { get; } = new Extent(0, 0);

    /// <summary>
    /// The largest room there is: 14,400 points each way, which is 200 inches. The PDF standard sets that as the
    /// largest side a page may have, so no page can be larger, and content measured in this room is measured as if
    /// nothing held it back.
    /// </summary>
    public static Extent Max { get; } = new Extent(14_400, 14_400);

    /// <summary>
    /// Whether either side is below zero by more than <see cref="Epsilon"/>: what is left of a room once more was
    /// taken from it than it had.
    /// </summary>
    public bool IsNegative => Width < -Epsilon || Height < -Epsilon;

    public Extent WithWidth(float width) => new Extent(width, Height);

    public Extent WithHeight(float height) => new Extent(Width, height);

    /// <summary>
    /// Whether this size fits in <paramref name="available"/> room: neither side larger by more than
    /// <see cref="Epsilon"/>.
    /// </summary>
    public bool FitsIn(Extent available) =>
        Width <= available.Width + Epsilon && Height <= available.Height + Epsilon;
}
