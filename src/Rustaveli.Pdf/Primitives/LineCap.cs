namespace Rustaveli.Pdf;

/// <summary>How the open ends of a stroke, and of every dash in it, are finished.</summary>
public enum LineCap
{
    /// <summary>Square, exactly at the end.</summary>
    Butt,

    /// <summary>Rounded, a half circle past the end.</summary>
    Round,

    /// <summary>Square, half the stroke's weight past the end.</summary>
    Square,
}
