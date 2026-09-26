using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds content sandwiched between two repeating bands.
/// </summary>
public sealed class BandsComposer(BandsBlock element)
{
    /// <summary>The band drawn above the content on every page.</summary>
    public IFrame Head() => element.Before;

    /// <summary>The flowing content between the bands.</summary>
    public IFrame Body() => element.Content;

    /// <summary>The band drawn below the content on every page.</summary>
    public IFrame Foot() => element.After;
}
