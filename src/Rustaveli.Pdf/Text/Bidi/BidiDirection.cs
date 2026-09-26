namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>How the direction of a paragraph is decided.</summary>
internal enum BidiDirection
{
    /// <summary>From the paragraph's first strong character, left to right when it has none (rules P2 and P3).</summary>
    Auto,

    /// <summary>Left to right whatever the text holds, as a document or frame direction sets it (HL1).</summary>
    LeftToRight,

    /// <summary>Right to left whatever the text holds, as a document or frame direction sets it (HL1).</summary>
    RightToLeft,
}
