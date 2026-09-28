using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>Names its content, so a layout failure inside it says where, by that name.</summary>
internal sealed class LabelBlock : EnclosingBlock
{
    public required string Label { get; init; }
}
