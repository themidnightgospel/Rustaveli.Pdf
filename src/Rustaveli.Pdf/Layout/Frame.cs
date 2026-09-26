namespace Rustaveli.Pdf.Layout;

/// <summary>
/// A transparent container used purely as an attachment point. Composing the fluent API produces a chain of
/// these, which keeps every builder method uniform: it sets a child and hands back a new slot.
/// </summary>
public sealed class Frame : EnclosingBlock
{
}
