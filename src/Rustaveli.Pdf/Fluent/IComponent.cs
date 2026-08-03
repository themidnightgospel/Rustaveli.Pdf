using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// A reusable piece of document structure.
/// </summary>
/// <remarks>
/// Components are the unit of reuse: they compose into a container exactly as inline code would, so a component
/// can be dropped anywhere an element is accepted without the surrounding layout treating it differently.
/// </remarks>
public interface IComponent
{
    void Compose(IContainer container);
}
