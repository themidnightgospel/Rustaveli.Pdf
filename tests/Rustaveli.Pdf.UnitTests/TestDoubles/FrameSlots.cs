namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Reaches the block a frame holds, so a test can set a double of its own into a frame; the public API sets only
/// the library's content.
/// </summary>
internal static class FrameSlots
{
    public static IFrameSlot Slot(this IFrame frame) => FrameAttachment.Slot(frame);
}
