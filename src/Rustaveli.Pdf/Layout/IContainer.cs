using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// A slot in the document tree that the fluent API attaches elements to.
/// </summary>
public interface IContainer
{
    Element? Child { get; set; }
}
