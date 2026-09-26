using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Changes the style text inherits, for everything beneath it.
/// </summary>
public sealed class DefaultTypeBlock : EnclosingBlock
{
    public Func<TypeStyle, TypeStyle> Refinement { get; set; } = style => style;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        WithStyle(context, () => base.Plan(availableSpace, context));

    public override void Render(Extent availableSpace, RenderContext context) =>
        WithStyle(context.Layout, () =>
        {
            base.Render(availableSpace, context);
            return true;
        });

    private T WithStyle<T>(PlanContext context, Func<T> function)
    {
        TypeStyle previous = context.DefaultTextStyle;
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
