using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds content sandwiched between two repeating bands.
/// </summary>
public sealed class DecorationDescriptor(BandsBlock element)
{
    /// <summary>The band drawn above the content on every page.</summary>
    public IFrame Before() => element.Before;

    /// <summary>The flowing content between the bands.</summary>
    public IFrame Content() => element.Content;

    /// <summary>The band drawn below the content on every page.</summary>
    public IFrame After() => element.After;
}
