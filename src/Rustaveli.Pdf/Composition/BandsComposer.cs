using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds content sandwiched between two repeating bands.
/// </summary>
public sealed class BandsComposer
{
    private readonly BandsBlock _block;

    internal BandsComposer(BandsBlock block) => _block = block;

    /// <summary>The band drawn above the content on every page.</summary>
    public IFrame Head() => _block.Head;

    /// <summary>The flowing content between the bands.</summary>
    public IFrame Body() => _block.Body;

    /// <summary>The band drawn below the content on every page.</summary>
    public IFrame Foot() => _block.Foot;
}
