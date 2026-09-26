namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A rectangle in absolute page space, with the active transform applied to both of its corners.
/// </summary>
/// <remarks>
/// <see cref="DrawOperation.Position"/> resolves only the origin, so it cannot see a scale — a shape drawn at
/// half size and a shape drawn at full size record the same position and the same untransformed
/// <c>Size</c>. Asserting on <see cref="Bounds"/> is what makes scaling, and the negative scale a flip is built
/// from, observable at all.
/// <para>
/// Derived from two opposite corners, so under rotation this is the rotated rectangle's extent rather than a
/// true bounding box. That is exact for the quarter turns the library supports.
/// </para>
/// </remarks>
internal readonly record struct Bounds(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;

    public float Height => Bottom - Top;
}
