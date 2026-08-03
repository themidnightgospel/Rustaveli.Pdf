using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Changes the style text inherits, for everything beneath it.
/// </summary>
public sealed class DefaultTextStyleElement : ContainerElement
{
    public Func<TextStyle, TextStyle> Refinement { get; set; } = style => style;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        WithStyle(context, () => base.Measure(availableSpace, context));

    public override void Draw(Size availableSpace, DrawContext context) =>
        WithStyle(context.Layout, () =>
        {
            base.Draw(availableSpace, context);
            return true;
        });

    private T WithStyle<T>(LayoutContext context, Func<T> function)
    {
        TextStyle previous = context.DefaultTextStyle;
        context.DefaultTextStyle = Refinement(previous);

        try
        {
            return function();
        }
        finally
        {
            context.DefaultTextStyle = previous;
        }
    }
}
