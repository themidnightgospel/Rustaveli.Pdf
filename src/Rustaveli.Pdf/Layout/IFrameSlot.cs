namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The engine's side of a public <see cref="IFrame"/>: the one block the frame holds.
/// </summary>
internal interface IFrameSlot : IFrame
{
    Block? Child { get; set; }
}
