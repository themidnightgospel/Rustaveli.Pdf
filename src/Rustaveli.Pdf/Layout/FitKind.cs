namespace Rustaveli.Pdf.Layout;

/// <summary>What a block would do with the room it was offered, as <see cref="Fit"/> records it.</summary>
internal enum FitKind
{
    /// <summary>All of the block has been drawn already, so it takes no room and draws nothing.</summary>
    Nothing,

    /// <summary>
    /// The block cannot be drawn in this room. It takes none, and a fresh page with more room might be able to hold it.
    /// </summary>
    Defer,

    /// <summary>The block draws some of its content here, and the rest continues on the next page.</summary>
    Partial,

    /// <summary>The block draws all that is left of it here.</summary>
    Complete
}
