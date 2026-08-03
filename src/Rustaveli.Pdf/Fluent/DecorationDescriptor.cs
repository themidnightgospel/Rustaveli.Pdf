using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds content sandwiched between two repeating bands.
/// </summary>
public sealed class DecorationDescriptor(DecorationElement element)
{
    /// <summary>The band drawn above the content on every page.</summary>
    public IContainer Before() => element.Before;

    /// <summary>The flowing content between the bands.</summary>
    public IContainer Content() => element.Content;

    /// <summary>The band drawn below the content on every page.</summary>
    public IContainer After() => element.After;
}
