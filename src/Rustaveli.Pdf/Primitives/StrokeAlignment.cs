namespace Rustaveli.Pdf;

/// <summary>
/// Where a frame's stroke lies against its edge — the stroke alignments of a page-layout application.
/// </summary>
public enum StrokeAlignment
{
    /// <summary>Within the frame, its outer edge on the frame's. The default: it never reaches past the frame.</summary>
    Inside,

    /// <summary>Centred on the frame's edge, half within and half beyond.</summary>
    Center,

    /// <summary>Beyond the frame, its inner edge on the frame's, so the content keeps its whole area.</summary>
    Outside,
}
