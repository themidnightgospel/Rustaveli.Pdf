namespace Rustaveli.Pdf;

/// <summary>An ink at a position along a gradient, from 0 at its start to 1 at its end.</summary>
/// <param name="Position">How far along the blend the ink lies.</param>
/// <param name="Ink">The ink there.</param>
public readonly record struct GradientStop(float Position, Ink Ink);
