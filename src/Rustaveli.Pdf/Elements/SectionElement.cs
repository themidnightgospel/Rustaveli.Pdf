using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Marks its child as a named destination that internal links can target.
/// </summary>
/// <remarks>
/// Registering the page number as a side effect of drawing is what lets a table of contents resolve targets:
/// the counting pass records where each section landed, and the drawing pass can then reference it.
/// </remarks>
public sealed class SectionElement : EnclosingBlock
{
    public string Name { get; set; } = string.Empty;

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (!string.IsNullOrEmpty(Name))
        {
            context.Page.RegisterDestination(Name, context.Page.CurrentPage);
            context.Canvas.DrawDestination(Name);
        }

        base.Draw(availableSpace, context);
    }
}
