namespace Rustaveli.Pdf.Primitives;

/// <summary>How an <see cref="Ink" /> is specified.</summary>
public enum InkModel
{
    /// <summary>Red, green and blue light, as a screen shows colour.</summary>
    Rgb,

    /// <summary>Cyan, magenta, yellow and black process inks, as a printing press lays them down.</summary>
    Cmyk,

    /// <summary>A named ink mixed in advance and printed on its own plate, with a process-colour fallback.</summary>
    Spot
}
