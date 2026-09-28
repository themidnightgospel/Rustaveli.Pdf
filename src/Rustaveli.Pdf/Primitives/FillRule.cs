namespace Rustaveli.Pdf;

/// <summary>Which parts of a path that crosses itself, or holds figures within figures, count as inside.</summary>
public enum FillRule
{
    /// <summary>Inside wherever the path winds around a point more one way than the other: an inner figure drawn the other way is a hole.</summary>
    NonZero,

    /// <summary>Inside wherever a ray from a point crosses the path an odd number of times: every inner figure is a hole.</summary>
    EvenOdd,
}
