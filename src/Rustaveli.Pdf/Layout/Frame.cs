namespace Rustaveli.Pdf.Layout;

/// <summary>
/// A frame: a block that adds nothing of its own and only holds the content placed into it. Composing produces a
/// chain of frames, which keeps every composing method uniform: it places a block into one frame and hands back the
/// frame inside that block.
/// </summary>
internal sealed class Frame : EnclosingBlock
{
}
